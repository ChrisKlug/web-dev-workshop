using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProductsClient("https://products");
builder.Services.AddHttpForwarderWithServiceDiscovery();
builder.Services.AddControllers();
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
app.MapDefaultEndpoints();

app.Map("/api/{**catch-all}", (HttpContext ctx) => {
    ctx.Response.StatusCode = 404;
});
app.MapForwarder("/{**catch-all}", "https+http://ui");

app.Run();
