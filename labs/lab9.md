# Lab 9: Creating an SDK for the Products API

HTTP-based APIs are fairly easy to consume. However, they do require a lot of boilerplate code in a lot of cases. And on top of that, exposing an HTTP-based API to the consumer might unfortunately also lock you down a bit, and limit what changes you can add to the API.

If you instead distribute an SDK that leverages the API, you have more control. It allows you to make changes in a different way, as the end-user hasn't built specialized code for the API in itself. It also makes it easier for the end-user to use the API.

## Steps (for Visual Studio)

### Create a new class library

The first step is to create a new __Class Library Project__ called __WebDevWorkshop.Services.Products.Client__. This will be the project that would be disributed to the end-user using for example NuGet. 

In this case, as you are not going to distribute it to anyone else, it will be a simple class library that other projects can use.

### Create the interface for the API service

Once the project has been created, rename the __Class1.cs__ file to __IProductsClient__. And turn the `Class1` class into an interface called __IProductsClient__.

```csharp
public interface IProductsClient
{
}
```

Now, as the data that comes back from the API is JSON, you need to turn it into a Plain Old C# Object (POCO). In this case, a simple `record` called __Product__.

__Note:__ It will look a lot like the `Product` in the __WebDevWorkshop.Services.Products__ project. However, you want to keep a total separation between these 2 projects. So, the SDK project needs its own representation of a product.

As this is a very simple interface with a single type connected to it, you might as well put it in the same file...

```csharp
public interface IProductsClient
{
}

public record Product(int Id,
    string Name,
    string Description,
    decimal Price,
    bool IsFeatured,
    string ThumbnailUrl,
    string ImageUrl);
```

The interface itself needs 2 methods to represent the 2 API endpoints. One called __GetProduct__ and one called __GetFeaturedProducts__. And both need to be async and return `Task`-based results.

```csharp
public interface IProductsClient
{
    Task<Product?> GetProduct(int productId);
    Task<Product[]> GetFeaturedProducts();
}
```

### Create an HTTP-based implementation

Now that the interface has been defined, it is time to create an implementation.

Create a new class called __HttpProductsClient__. Make it internal. And make it inherit `IProductsClient`

```csharp
internal class HttpProductsClient : IProductsClient
{
    public Task<Product?> GetProduct(int productId)
    {
    }

    public Task<Product[]> GetFeaturedProducts()
    {
    }
}
```

As this implementation will use HTTP requests to get the data, it will need to get an `HttpClient` injected in the constructor.

```csharp
internal class HttpProductsClient(HttpClient httpClient) : IProductsClient
```

Implementing the `GetFeaturedProducts` method is fairly easy. You just need to use the `HttpClient` to make a __GET__ request to the __/api/products/featured__ endpoint, and then deserialize the response to `Product[]`.

To make it even easier, there is an extension method for `HttpClient` called `GetFromJsonAsync()`. This does everything you need. So the implementation simply becomes

```csharp
public Task<Product[]> GetFeaturedProducts()
    => httpClient.GetFromJsonAsync<Product[]>("/api/products/featured")!;
```

__Note:__ This would obviously be a bit more complicated if you needed authentication etc. But the good thing for the consumer of the SDK is that they wouldn't need to care about that. You could just pass in the required information in this class, and it could handle it all for them.

The `GetProduct()` method is a bit more complicated, as it returns a `Product?`. However, the API indicates a `null` response through an HTTP 404 status code. So, you will need to use the simpler `GetAsync()` method to get the `HttpResponseMessage` to be able to verify the returned status code. And if the status code is 404 Not Found, you return null.

```csharp
public async Task<Product?> GetProduct(int productId)
{
    var response = await httpClient.GetAsync("api/products/" + productId);

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        return null;
    
}
```

__Note:__ The method also need to be made `async` to support the use of `await`

Once you know that the status code is not 404, you know that the response should contain a product.

You probably want to check that it is a 200, and handle other potential problems here as well. But...this is a simple lab, not production ready code... 😂

So, if you assume everything is OK, you just need to deserialize the response and return it. Unfortunately, deserializing it using `System.Text.Json.JsonSerializer`, which is the recommended approach, is a bit fiddly. By default, it is not case-insensitive when i comes to property names. So, to be able to use the camelCased JSON, you need to provide it with an instance of `JsonSerializerOptions` with the `PropertyNameCaseInsensitive` property set to `true`

```csharp
return JsonSerializer.Deserialize<Product>(
            await response.Content.ReadAsStringAsync(), 
            new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            });
```

### Simplifying the use of the SDK

SDK-style projects should be easy to use. And when using it in .NET, it is very likely it will be added to DI. But, instead of requiring the user to manually register the interface and implementation, you can help them do it.

__Note:__ In this case, the end-user wouldn't even be able to do it themselves, as the `HttpProductsClient` class is `internal`

The best way to sort this out, is to provide an extension method that does the work. However, to do this, you need to reference 2 NuGet packages 

- Microsoft.Extensions.DependencyInjection.Abstractions
- Microsoft.Extensions.Http

The first one is to allow you to use the `IServiceCollection` interface. And the second to allow you to add the `HttpClientFactory` service to the DI.

__Note:__ Adding the `HttpClientFactory` isn't strictly necessary as long as the end-user registers an `HttpClient` in the IoC container. However, it is very likley that they will not do that, so adding the `HttpClientFactory` ensures that the `HttpClient` is available for injection.

Once the NuGet packages have been installed, you need to create a new, static class called __IServiceCollectionExtensions__. 

```csharp
public static class IServiceCollectionExtensions
{   
}
```

The extension method should extend the `IServiceCollection` interface, and return the same. The name __AddProductsClient__ follows the standard of using __AddXXX___

```csharp
public static IServiceCollection AddProductsClient(this IServiceCollection services)
{
    return services;
}
```

The first thing you need to do, is to register the `IProductsClient` as an HttpClient using the `HttpProductsClient` as the implementation. This will make sure that whenever someone requests an `IProductsClient`, they will get an `HttpProductsClient`. And the `HttpProductsClient` will automatically get an `HttpClient` injected

```csharp
public static IServiceCollection AddProductsClient(this IServiceCollection services)
{
    services.AddHttpClient<IProductsClient, HttpProductsClient>();
    return services;
}
```

However, you also need to tell the `HttpClient` where the actual API is located, as the `HttpProductsClient` only uses relative URLs. And, you need to provide the end-user a way to do this.

A standard way to do this, is to provide the user with a configuration callback. In the callback, the user is given a class on which they can configure the base address.

This pattern works very well for optional configuration. In this case, the base address isn't optional. So, you might as well just add a second, string parameter called __baseAddress__ to the `AddProductsClient()` method

```csharp
public static IServiceCollection AddProductsClient(
        this IServiceCollection services,
        string baseAddress)
```

You can then use the client configuration callback in the `AddHttpClient<T,Y>()` call, to set the base address for the `HttpClient` used when creating the `HttpProductsClient` instance.

```csharp
services.AddHttpClient<IProductsClient, HttpProductsClient>(client => {
    client.BaseAddress = new Uri(baseAddress);
});
```

Now, any relative path used by the `HttpProductsClient` will be prefixed with the provided base address.

That's all there is to it! In the next lab you will see how to use this SDK to retrieve products from the API, without having to use HTTP requests.

[<< Lab 8](./lab8.md) | [Lab 10 >>](./lab10.md)