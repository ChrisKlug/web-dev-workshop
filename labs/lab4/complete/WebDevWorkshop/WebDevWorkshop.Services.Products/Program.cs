using Microsoft.EntityFrameworkCore;
using WebDevWorkshop.Services.Products.Data;
using static WebDevWorkshop.Services.Products.Data.IProducts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IProducts, EfProducts>();
builder.AddSqlServerDbContext<ProductsContext>("WebDevWorkshop");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    using (var ctx = scope.ServiceProvider.GetRequiredService<ProductsContext>())
    {
        ctx.Database.Migrate();
        var sql = File.ReadAllText("Data/SeedData.sql");
        ctx.Database.ExecuteSqlRaw(sql);
    }
}

app.MapGet("/", () => "Hello World!");

app.Run();
