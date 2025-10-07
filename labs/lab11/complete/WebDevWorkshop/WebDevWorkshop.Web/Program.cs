using Microsoft.Extensions.FileProviders;
using WebDevWorkshop.Services.Products.Client;
using WebDevWorkshop.Web.Models;
using WebDevWorkshop.Web.ShoppingCart;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProductsClient("https://products");

builder.Services.AddHttpForwarderWithServiceDiscovery();

builder.Services.AddControllers();

builder.Services.AddOrleans(silo => {
    silo.UseLocalhostClustering();
});

builder.AddServiceDefaults();

var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/images/products",
    FileProvider = new PhysicalFileProvider(
        Path.Combine(
            app.Environment.ContentRootPath,
            "ProductImages")
    )
});

app.UseRouting();
app.MapControllers();

//Minimal APIs
app.MapPost("/api/shopping-cart", async (AddShoppingCartItemModel model, HttpContext ctx,
            IProductsClient productsClient, IGrainFactory grainFactory) =>
{
    var product = await productsClient.GetProduct(model.ProductId);
    if (product is null)
    {
        return Results.BadRequest();
    }
    string cartId;
    if (ctx.Request.Cookies.ContainsKey("ShoppingCartId"))
    {
        cartId = ctx.Request.Cookies["ShoppingCartId"]!;
    }
    else
    {
        var rnd = new Random();
        cartId = new string(Enumerable.Range(0, 30)
            .Select(x => (char)rnd.Next('A', 'Z'))
            .ToArray());
        ctx.Response.Cookies.Append("ShoppingCartId", cartId);
    }
    var cart = grainFactory.GetGrain<IShoppingCart>(cartId);
    await cart.AddItem(new ShoppingCartItem
    {
        ProductId = product.Id,
        ProductName = product.Name,
        Price = product.Price,
        Count = model.Count
    });
    return Results.Ok(await cart.GetItems());
});
app.MapGet("/api/shopping-cart", async (HttpContext ctx, IGrainFactory grainFactory) =>
{
    if (ctx.Request.Cookies.ContainsKey("ShoppingCartId"))
    {
        var cart = grainFactory.GetGrain<IShoppingCart>(
        ctx.Request.Cookies["ShoppingCartId"]
        );
        return Results.Ok(await cart.GetItems());
    }
    return Results.Ok(Array.Empty<ShoppingCartItem>());
});

app.MapDefaultEndpoints();

app.Map("/api/{**catch-all}", (HttpContext ctx) => {
    ctx.Response.StatusCode = 404;
});

app.MapForwarder("/{**catch-all}", "https+http://ui");

app.Run();
