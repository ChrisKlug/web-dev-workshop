# Lab 5: Implementing the Products API using FastEndpoints

The Products API should be a REST-ful HTTP-based API, which in a lot of cases means implementing it using ASP.NET MVC or minimal APIs. However, MVC is slowly falling out of focus because of the extra ceremony needed. As well as because of them being slightly less efficient than minimal APIs. 

Minimal APIs on the other hand lack a built in mechanism for grouping and managing larger quantities of endpoints. They are also often implemented using a mediator pattern, using for example MediatR, to make them easier to test and manage.

Another, a bit less used way of implementing HTTP-based APIs, is to implement them using a project called [FastEndpoints](https://fast-endpoints.com). This project uses minimal APIs as its foundation, but adds a mediator type of pattern on top of it. This gives you the best of both worlds in some ways. You get the chain of responsibility pattern support by using the mediator pattern. It adds support for grouping and structure when using large amounts of endpoints. And it has the performance benefits of minimal APIs. It also supports all the features minimal APIs support, as well as a lot of those supported by MVC as well.

## Steps (for Visual Studio)

### Add Fast Endpoints to the project

To add support for Fast Endpoints in your __WebDevWorkshop.Services.Products__ project, you start by adding a reference to the NuGet package __FastEndpoints__.

Once the NuGet package has been added, you need to add the required services to DI. This is eaisly done by calling the `AddFastEndpoints()` extension method on the `WebApplicationBuilder`.

```csharp
builder.Services.AddFastEndpoints()
```

And finally you have to add it to the request pipeline as well. While doing so, you might as well remove the default "Hello World" endpoint as well.

```csharp
app.MapGet("/", () => "Hello World!"); // Remove this

app.UseFastEndpoints();
```

### Add the "featured products" endpoint

Now that Fast Endpoints support has been added to the project, you need to add the first endpoint. You will start with the "featured products" endpoint.

To add a new endpoint, you need to add a class that inherits from `Endpoint<T,Y>` or one of its decendants. 

However, to keep a bit of structure in the project, you should add a new directory called __Endpoints__ in the __WebDevWorkshop.Services.Products__ project first. And then add a new class called __FeaturedProductsEndpoint__ in this directory.

As this endpoint has no "incoming information", that is no path parameters or query/form parameters, it can inherit from `EndpointWithoutRequest<T>`. 

__Note:__ The `T` defines what this endpoint returns. Information that can be used to enhance the metadata for this endpoint. Metadata that can then be used by things like Swagger etc to generate API documentation. 

As this endpoint shoild return an HTTP 200 OK, and an array of `Product`, you can set the `T` to `Ok<Product[]>`.

```csharp
public class FeaturedProductsEndpoint
    : EndpointWithoutRequest<Ok<Product[]>>
{   
}
```

To tell "the system" that this endpoint responds to __GET__ request at the path __/api/products/featured__, you have 2 options. You can use the attribute-based way that MVC uses, adding `[HttpGet('/api/products/featured')]` to the class. Or, you can use the way that is more native to Fast Endpoints, which is to override the `Configure()` method and call the `Get()` method.

```csharp
public class FeaturedProductsEndpoint
    : EndpointWithoutRequest<Ok<Product[]>>
{
    public override void Configure()
    {
        Get("/api/products/featured");
    }
}
```

By default, Fast Endpoints expects users to be authenticated. However, this endpoint should be available even if you aren't logged in.

__Note:__ You also don't have authentication in place, so the endpoint would actually never be able to be called as it sits.

To disable authentication, you can once again do one of two things. Either add the `[AllowAnonymous]` attribute, or call `AllowAnonymous()` in the `Configure()` method. 

```csharp
public override void Configure()
{
    Get("/api/products/featured");
    AllowAnonymous();
}
```

To handle the incoming request, you have once again 2 options. Either you override the `HandleAsync()` method, or the `ExecuteAsync()` one. The `HandleAsync()` one requires you to manually send back the response. While the `ExecuteAsync()` one let's you simply return a response type from the method.

In this case, overriding the `ExecuteAsync()` method is the easiest.

```csharp
public override Task<Ok<Product[]>> ExecuteAsync(CancellationToken ct)
{
    
}
```

The problem is that we can't get hold of the required products without having access to the `IProducts` repository. Luckily, Fast Endpoints (quite obviously) supports dependency injection. So, you just need to add the interface as a parameter to the constructor

```csharp
public class FeaturedProductsEndpoint(IProducts products)
    : EndpointWithoutRequest<Ok<Product[]>>
{
    ...
}
```

And with that in place, implementing the `ExecuteAsync()` method is simply a matter of making the method `async`, calling the repository to get the products, and return the products in an `Ok<T>`

```csharp
public override async Task<Ok<Product[]>> ExecuteAsync(CancellationToken ct)
    => TypedResults.Ok(await products.ThatAreFeatured());
```

### Add the "product" endpoint

The second endpoint should return a specific `Product`. For this, the path will include a path parameter that the endpoint will read to figure out what id to use.

Start by adding a new class called __ProductEndpoint.cs__. However, before you can make it inherit from `Endpoint`, you will need one more class.

Fast Endpoints use classes/records to represent the incoming data (path and query parameters etc). To support this, create a record called __Request__ inside the `ProductEndpoint` class. The record needs a single `ìnt` property called __Id__

```csharp
public class ProductEndpoint
{
    public record Request(int Id);
}
```

Now, you can make the `ProductEndpoint` class inherit from `Endpoint<T, Y>`.

In this case, `T`represents the "request" class. The class that Fast Endpoints will bind the incoming data to. The `Y`represents the returned type. Just like in the `FeaturedProductsEndpoint`.

The `T` is `ProductEndpoint.Request`. However, the `Y` is a bit more complicated, as this endpoint might return either an HTTP 200 OK and a `Product`, or a 404 Not Found. To represent that in code, you can use the `Results<T,Y>` class. This class allows you to specify more than one return type. In this case, it will be `Results<Ok<Product>, NotFound>>`

```csharp
public class ProductEndpoint
    : Endpoint<
        ProductEndpoint.Request, 
        Results<Ok<Product>, NotFound>
    >
{
    ...
}
```

And just as in the previous endpoint, you need to get hold of the `IProducts` implementation

```csharp
public class ProductEndpoint(IProducts products)
    ...
```

Now, with that in place, we need to override the `Configure()` method, and the `ExecuteAsync()` method.

The `Configure()` method looks very similar to the one you just created. However, the path includes a path parameter. And path parameters are defined using the same syntax as in ASP.NET Core

```csharp
public override void Configure()
{
    Get("/api/products/{id}");
    AllowAnonymous();
}
```

And because the parameter is named `id`, it will automatically map towards the `Id` property on the `Request` instance that is provided in the `ExecuteAsync` method.

The `ExecuteAsync` method looks a little different when you inherit from `Endpoint` instead of `EndpointWithoutRequest`. The difference being that it has one more parameter of the type specified as `T` for the base class

```csharp
public override Task<Results<Ok<Product>, NotFound>> ExecuteAsync(
    Request req, 
    CancellationToken ct)
{
    
}
```

The implementation is almost as simple as in the previous endpoint. You just need to retrieve the requested product and return it. However, as this product might not exist, you need to make sure you send a 404 Not Found if the repository return null

```csharp
public override async Task<Results<Ok<Product>, NotFound>> ExecuteAsync(
    Request req, 
    CancellationToken ct)
{
    var product = await products.WithId(req.Id);
    
    return product is not null 
            ? TypedResults.Ok(product)
            : TypedResults.NotFound();
}
```

__Note:__ Don't forget to mark the method as `async`

### Verify that it works

The last step is to verify that the endpoints work.

__Note:__ Fast Endpoints automatically registers any endpoint in DI during start up, so you don't need to do anything else to make it work...

Press __F5__ to start debugging. This should open up 3 tabs, one for the Dashboard, one for the Web project and one for the Products service.

Find the tab that points to the Products service, and browse to __/api/products/featured__ and to __/api/products/1__, to verify that you are getting the expected data back.

[<< Lab 4](../lab4/lab4.md) | [Lab 6 >>](../lab6/lab6.md)