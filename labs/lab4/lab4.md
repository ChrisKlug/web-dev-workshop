# Lab 4 - Creating a Products Service

Now that you have a database in place, it is time to create a new Products service that will be responsible for serving up the products information. It will do so using an HTTP-based API using FastEndpoints. However, before you can get to the API creation, you first need a new project, and some database access.

## Steps (for Visual Studio)

### Add a new project

Add a new project to your solution. Make it an __ASP.NET Core Empty__ project, and call it __WebDevWorkshop.Services.Products__.

Making sure that "_Enlist in .NET Aspire orchestration_" option is ticked on the second screen to add it to Aspire.

Once the new project has been set up. You should be able to open the __AppHost.cs__ file in the __WebDevWorkshop.AppHost__ project and see the new project being added to the AppModel.

### Add a reference to the database

The next step is to add a reference to the __WebDevWorkshop__ database by calling the `WithReference()` method.

```csharp
builder.AddProject<Projects.WebDevWorkshop_Services_Products>("webdevworkshop-services-products")
    .WithReference(db);
```

However, when referencing another resource, especially one that might take some time to start, it is a good idea to also wait for that resource to become available. A little tweak that is easily solved by adding a call to the `WaitFor()` method, passing in the resource you want to wait for.

```csharp
builder.AddProject<Projects.WebDevWorkshop_Services_Products>("webdevworkshop-services-products")
    .WithReference(db)
    .WaitFor(db);
```

### Verify that it works

With that code in place, you can press __F5__ to start debugging.

To verify that the reference has worked as well, you can click on the __webdevworkshop-services-products__ resource row to show the details for that resource. You can then scroll down in the details pane to the __Environment Variables__ section. There, you should find an environment variable called __ConnectionStrings__WebDevWorkshop__. To view the actual value, you can either show all values, by clicking the "Show Values" button (![Show Values](./resources/show-values-button.png)), or click on the inspect button (![Inspect Button](./resources/inspect-button.png)) to the right of the ●●●●●●●●.

__Note:__ The password is auto-generated for you unless you manually provide one, which isn't really necessary in this case.

__Note 2:__ The password is also marked as sensitive by default, so it won't show up in logs etc.

### Create the Product entity

The service is going to be really simple, and the data model consists of a single type called __Product__. 

However, adding classes randomly to the root of the project is not the best structure. Instead, start by creating a directory called __Data__. Inside the __Data__ directory, add a new C# record called __Product__.

It needs a few properties, like this

```csharp
public record Product(int Id, 
    string Name, 
    string Description, 
    decimal Price, 
    bool IsFeatured, 
    string ThumbnailUrl, 
    string ImageUrl);
```

### Create a new DbContext

Before you can start accessing the database, and pull out products using EntityFramework, you need a `DbContext`. And to be able to create a `DbContext`, you need to reference an EntityFramework NuGet package. 

However, when using Aspire, the recommended NuGet package is the `Aspire.Microsoft.EntityFrameworkCore.SqlServer`, instead of the "normal" `Microsoft.EntityFrameworkCore.SqlServer` package. So, go ahead and add a reference to the `Aspire.Microsoft.EntityFrameworkCore.SqlServer` NuGet package.

Next, create a __ProductsContext__ class in the __Data__ directory.

Make the new class inherit from `DbContext`, and accept a `DbContextOptions<ProductsContext>` parameter that is passed to the base class.

```csharp
public class ProductsContext(DbContextOptions<ProductsContext> options)
    : DbContext(options)
{
}
```

Now that you have the `DbContext`, you can add it to DI by opening the __Program.cs__ file and call the `AddSqlServerDbContext<T>()` extension method on the `builder`. The method takes a string parameter that should be the name of the connectionstring to use. 

As we configured the database to be called __WebDevWorkshop__ when we set it up in Aspire, the connectionstring name will be that. 

```csharp
builder.AddSqlServerDbContext<ProductsContext>("WebDevWorkshop");
```

### Setting up the database

Currently you have a completely empty database. To set up the structure, you can use EF migrations.

You will only have a single migration in this project, but it might still be a good idea to separate them out, in case there might be more in the future.

Add a new directory called __Migrations__ under the __Data__ directory. Then add a new class called __InitialMigration.cs__ in the __Migrations__ directory.

The migration doesn't really matter, so you can simply replace the `InitialMigration` class with the following

```csharp
[Migration("001_InitialMigration")]
[DbContext(typeof(ProductsContext))]
public class InitialMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>().Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(maxLength: 128),
                    Description = table.Column<string>(),
                    Price = table.Column<decimal>(),
                    IsFeatured = table.Column<bool>(),
                    ThumbnailUrl = table.Column<string>(),
                    ImageUrl = table.Column<string>(),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                }
            );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Products");
    }
}
```

### Running the migrations at start up

Now that you have a migration, it needs to be run. And during development, the simplest way to do this, is to do it during application start up.

Open the __Program.cs__ file, and add a conditional that verifies that you are in development mode right after the creation of the `WebApplication`.

```csharp
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
}
```

Now, the problem here, is that the `ProductsContext` is registered as `Scoped` by default. And at this point you don't have a HTTP-request, and thus no scope. Because of this, you have to create a container scope manually using `app.Scope.CreateScope()`. This returns a scoped container, which also implements `IDisposable`. So, you should really use a `using` statement for it

```csharp
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {

    }
}
```

Once you have a scope to work with, you just need to get hold of a `ProductsContext` and run the migrations

```csharp
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    using (var ctx = scope.ServiceProvider.GetRequiredService<ProductsContext>())
    {
        ctx.Database.Migrate();
    }
}
```

### Configuring the ProductsContext

Now you have a `DbContext` and a database table to store the `Product` entities in. All you need now, is to configure the `ProductsContext` to support querying for `Product` instances.

Open up the __ProductsContext.cs__ file, and add an override for the `OnModelCreating` method

```csharp
public class ProductsContext(DbContextOptions<ProductsContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}
```

Considering that the table you created with the migration has columns that correspond to the property names of your `Product` record, the entity mapping is really simple.

```csharp
modelBuilder.Entity<Product>(x => {
            x.ToTable("Products");
            x.HasKey(y => y.Id);
        });
```

### Adding seed data

Currently you have a database with an empty table. You probably want some seed data to work with. And even if there are some seed data support in EF Core, the simplest way is often to just use a SQL script.

Go ahead and copy [this file](./resources/SeedData.sql) to the __Data__ directory. 

Once you have the SQL file in place, you can tell EF to run the code for you by calling `ctx.Database.ExecuteSqlRaw()` right after you ran the migration. However, the method expects a string containing the SQL to run. So, you need to read the SQL file contents as a string first, and then pass that in. 

```csharp
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    using (var ctx = scope.ServiceProvider….)
    {
        ...
        var sql = File.ReadAllText("Data/SeedData.sql");
        ctx.Database.ExecuteSqlRaw(sql);
    }
}
```

### Creating a Products repository

Now that you have some data, and a way to access it, you can simplify the access by providing a simple repository. In this case, the application will only need 2 repository methods, `WithId(int id)` and `ThatAreFeatured()`. They should both be async, and return `Product?` and `Product[]`.

Add a new interface in the __Data__ directory called __IProducts__

```csharp
public interface IProducts
{
    Task<Product?> WithId(int id);
    Task<Product[]> ThatAreFeatured();
}
```

Then add a class called __EfProducts__ in the same file. It should implement the `IProducts` interface

```csharp
public interface IProducts
{
    ...
}

public class EfProducts : IProducts
{
    public Task<Product?> WithId(int id)
        => throw new NotImplementedException();

    public Task<Product[]> ThatAreFeatured()
        => throw new NotImplementedException();
}
```

The class is going to need access to the `ProductsContext`, so add a primary constructor parameter of that type called __ctx__

```csharp
public class EfProducts(ProductsContext ctx) : IProducts
{
    ...
}
```

To implement the `WithId()` method, use the `Set<T>()` method to get hold of a set of `Product`. Then use Linq to get the `Product` with the supplied id. However, as it might be and invalid id, you need to make sure the code can handle that. `FirstOrDefaultAsync()` handles this, so you can use that.

```csharp
public Task<Product?> WithId(int id) 
    => ctx.Set<Product>().FirstOrDefaultAsync(x => x.Id == id);
```

And the `ThatAreFeatured()` method needs to get all `Product` entities where `IsFeatured` is true

```csharp
public Task<Product[]> ThatAreFeatured()
    => ctx.Set<Product>()
        .Where(x => x.IsFeatured)
        .ToArrayAsync();
```

The last step in this lab is to add the repository to the DI. So, open up the __Program.cs__ file, and add the `IProducts` repository to the DI, using the `EfProducts` class as the implementation

```csharp
builder.Services.AddScoped<IProducts, EfProducts>();
```

[<< Lab 3](../lab3/lab3.md) | [Lab 5 >>](../lab5/lab5.md)