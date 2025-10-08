using Microsoft.EntityFrameworkCore;

namespace WebDevWorkshop.Services.Products.Data;

public class ProductsContext(DbContextOptions<ProductsContext> options)
: DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(x =>
        {
            x.ToTable("Products");
            x.HasKey(p => p.Id);
        });
    }
}
