# Lab 10: Providing the UI with Products

The UI is limited by CORS. The easiest way to handle this, is to use a backend for frontend (BFF) pattern. This basically means providing an single API for the frontend to use, that is specifically built for the frontend. And by placing on the same domain as the frontend, security is also a lot simpler, as cookie based authentication works.

So, to do that in this project, you already have the __WebDevWorkshop.Web__ project. Here you can add whatever API you need for the frontend. And to the frontend (the UI project), it will look as if it is hosted on the same host, as it also reverse proxies the frontend for you.

## Steps (for Visual Studio)

### Registering the IProducts service

You have to start by making the newly created `IProductsClient` service available in the web project. This should be fairly easy due to the work you have already done in the SDK-style client project.

Open the __WebDevWorkshop.Web__ and add a reference to the __WebDevWorkshop.Services.Products.Client__ project.

__Note:__ As mentioned before, this would normally be distributed as a NuGet package instead of a project reference.

Then open the __Program.cs__ file and call the `AddProductsClient()` extension method somewhere between the creation of the `WebApplicationBuilder` and the call to `Build()`.

```csharp
builder.Services.AddProductsClient();
```

This requires you to add a `using` statement at the top to invlude the `WebDevWorkshop.Services.Products.Client` namespace. 

This works, but it hides the extension method a bit. To fix this, you can open the __IServiceCollectionExtensions.cs__ file in the __WebDevWorkshop.Services.Products.Client__ project, and change the namespace to `Microsoft.Extensions.DependencyInjection`

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
...
```

__Note:__ You instead need to add a `using WebDevWorkshop.Services.Products.Client` in this class.

After doing this, you can go back to the __Program.cs__ file again. 

You will notice that the extension method is found now. That is because the `Microsoft.Extensions.DependencyInjection` namespace is included by default.

You are currently missing the `baseAddress` parameter in your call, which is fairly easily fixed when using Aspire. You just need to set it to __https://products__. Aspire service discovery will then make sure to replace the __products__ part with __localhost__ and the correct port. At least if you have configured a reference from the web project to the products service.

```csharp
builder.Services.AddProductsClient("https://products");
```

### Connecting the 2 projects

Currently, there is no reference from the web project to the products API in the Aspire AppModel. And because of this, the __https://products__ address will fail.

There are 2 things you need to do to fix this... The first one being to add the reference between the 2 resources.

Open up the __AppHost.cs__ file in the __WebDevWorkshop.AppHost__ project.

Start by making sure that the addition of the __webdevworkshop-services-products__ resource is placed before the addition of the __webdevworkshop-web__ project.

```csharp
builder.AddProject<Projects.WebDevWorkshop_Services_Products>("webdevworkshop-services-products")
    ...

builder.AddProject<Projects.WebDevWorkshop_Web>("webdevworkshop-web", "aspire")
    ...
```

You will also need a variable to hold the __webdevworkshop-services-products__ resource

```csharp
var products = builder.AddProject<Projects.WebDevWorkshop_Services_Products>("webdevworkshop-services-products")
    ...
```

Then you just need to add a reference from the __webdevworkshop-web__ project

```csharp
builder.AddProject<Projects.WebDevWorkshop_Web>("webdevworkshop-web", "aspire")
    ...
    .WithReference(products);
```

It might also be a good idea to wait for the product API to come online, as that in turn waits for the database to come online.

```csharp
builder.AddProject<Projects.WebDevWorkshop_Web>("webdevworkshop-web", "aspire")
    ...
    .WaitFor(products);
```

This adds the reference. However, the products client expcts the products service to respond to __https://products__. Right now, the URL is actually __https://webdevworkshop-services-products__, since the hostname corresponds to the name of the resource. To fix this, you just need to rename the resource to __products__

```csharp
var products = builder.AddProject<Projects.WebDevWorkshop_Services_Products>("products")
```

### Adding the HTTP-endpoints

Now that the `IProductsClient` is in place, you can focus on creating the HTTP endpoints that the frontend is supposed to call.

In this case, for different reasons, the choice has fallen on good old MVC.

So, the first thing you need to to is to add support for MVC controllers in the project.

Start by opening the __Program.cs__ file in the __WebDevWorkshop.Web__ project. 

Before you can add MVC controllers to the request pipeline, you need to add the required services by calling the `AddControllers()` extension method on the `IServiceCollection`. It needs to be performed before the application is built.

```csharp
builder.Services.AddControllers();
```

__Note:__ As you are only providing API endpoints, you can get away with just `AddControllers()` instead of the `AddControllersWithViews()`

Now that the services are in place, you can add the controller support to the request pipeline by calling `MapControllers()`. 

```csharp
var app = builder.Build();

app.MapControllers();

app.Map("/api/{**catch-all}", (HttpContext ctx) => {
    ...
});
```

Remember to do it before the other, existing calls, as these are meant to work as fallbacks when no controller has responded.

There is a small problem right now, and that is that MVC requires routing to be able to route to the correct controller. So, before the call to `MapControllers()`, you need to add a call to `UseRouting()` as well

```csharp
var app = builder.Build();

app.UseRouting();

app.MapControllers();
```

Ok, now it is time to focus on the actual MVC controllers!

Create a new directory called __Controllers__.

__Note:__ MVC Controllers do not actually need to be in a directory called __Controllers__. It is just a structural convention that has zero impact on the code. But it is what people are used too...

In the __Controllers__ directory, use the VS tooling to create a new controller called __ProductsController__ using the __API Controller Empty__ template. Or just a class called __ProductsController__ and have it inherit from `Controller`.

Add a primary contstructor that accepts an `IProductsClient` interface

```csharp
public class ProductsController(IProductsClient productsClient)
    : Controller
{
    ...
}
```

Depending on how you created the controller, you might need to add a `RouteAttribute` with the value __/api/[controller]__ to the class

```csharp
[Route("api/[controller]")]
public class ProductsController(IProductsClient productsClient)
    : Controller
{
    ...
}
```

This will make sure that any action in this controller will have __api/products/__ added as a prefix to its path.

Next you can replace the generated `Index` action with an action called __GetFeaturedProducts()__. It should be `async` and return `Task<Ok<Product[]>>`

```csharp
public async Task<Ok<Product[]>> GetFeaturedProducts()
{
    
}
```

To make this an action that responds to HTTP GET, you need to add an `HttpGetAttribute` to it, setting the path to __featured__

```csharp
[HttpGet("featured")]
public async Task<Ok<Product[]>> GetFeaturedProducts()
{
}
```

The actual implementation is simple. Since the `Product` you get back is already a DRO, you just need to await a call to the `IProductsClient.GetFeaturedProducts()` method, and return the response wrapped in an "OK"

```csharp
[HttpGet("featured")]
public async Task<Ok<Product[]>> GetfeaturedProducts()     
    => TypedResults.Ok(
         await productsClient.GetFeaturedProducts()
    );
```

__Note:__ You probably want a bit more error handling here, but once again, it is a lab. And also, you actually get retries on the HTTP request simply by using the Aspire `HttpClient` configuration.

The second endpoint needs to return a specific product. So, go ahead and add a new action that is called __GetProduct__. It should take a single `int` parameter called __productId__ and return a `Task<>Results<NotFound, Ok<Product>>`. Oh, and it needs to be async as well...

```csharp
public async Task<Results<NotFound, Ok<Product>>> GetProduct(int productId)
{
}
```

And to make it respond to the path __api/products/{productId}__, you need to add an `HttpGetAttribute` to this action as well

```csharp
[HttpGet("{productId}")]
public async Task<Results<NotFound, Ok<Product>>> GetProduct(int productId)
{
}
```

The implementation of this endpoint is a tiny bit more complicated, as it needs to return 404 if there is no product, in other words, if the `IProductsClient.GetProduct()` method returns `null`

```csharp
var product = await productsClient.GetProduct(productId);
return product is not null 
    ? TypedResults.Ok(product) 
    : TypedResults.NotFound();
```

Those are all the endpoints you need right now!

### Verify that it works

To verify that it works, you need to press __F5__ to start the Aspire project.

As the browser opens the web project in a tab, you will see that it actually fails...

There is a reason for this. And the reason for this is that the adress used for the products API is __https://products__. However, the products API isn't actually serving HTTPS requests.

There are 2 ways to fix this. Either, you can change the address to be __https+http://products__ to make it fallback to HTTP is HTTPS isn't available. Or, even better, you can simply make it respond to HTTPS.

### Adding HTTPS support

Open the __AppHost.cs__ file in the __WebDevWorkshop.AppHost__ project. Find the line where the __WebDevWorkshop.Services.Products__ project is added, and add a second parameter to the `AddProject<T>()` call. The value should be `"https"`. This tells Aspire to use the __https__ launch profile, which supports both HTTP and HTTPS.

```csharp
var products = builder.AddProject<Projects.WebDevWorkshop_Services_Products>("products", "https")
```

And while you are at it, you might as well change the products project so that it doesn't open a browser tab every time you start Aspire.

To do this, open the __Properties/launchSettings.json__ file in the __WebDevWorkshop.Services.Products__ project. 

Locate the __https__ profile, and set the `launchBrowser` setting to `false`.

### Verify that it works

Now, try start debugging again.

Now, you should get a browser with 2 tabs. One showing the Aspire Dashboard, and one showing the UI with actual products on it!

The images are supposed to be missing. This will be fixed in the next lab!


[<< Lab 9](./lab9.md) | [Lab 11 >>](./lab11.md)