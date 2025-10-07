# Lab 7: Clean up the Integration Testing


The integration test for the "Featured Products" endpoint is working. However, it is not very easy to read. And if you wanted to create more tests, there is a LOT of duplicated code that needs to be added.

So, to simplify this, you are going to move all of the boilerplate stuff into a helper class that will make the test a lot cleaner.

## Steps (for Visual Studio)

### Create a new class library

The first step is to add a new __Class Library Project__ to your solution. It would make sense calling it __WebDevWorkshop.Testing__.

### Create a TestHelper class

The project comes with a default __Class1__. Rename the file and class to __TestHelper__.

The `TestHelper` class should also be made static.

```csharp
public static class TestHelper { 

}
```


Now, before you can start moving some of the code from the test to this new helper, you are going to need to reference some NuGet packages. More specifically these:

- Microsoft.EntityFrameworkCore.SqlServer
- Microsoft.AspNetCore.Mvc.Testing

With those packages referenced, you can go ahead and add a static method called __ExecuteTest__ to your `TestHelper` class. 

The __ExecuteTest__ method should accept a `Func<DbCommand, Task>` called __dbSetup__ and a `Func<HttpClient, Task>` called __test__. It also needs to be async and return a Task.

```csharp
public static async Task ExecuteTest(
    Func<DbCommand, Task> dbSetup, 
    Func<HttpClient, Task> test)
{
}
```

You can now go ahead and copy over all the code from the `GET_Returns_HTTP_200_and_all_products_marked_as_featured` method into this new method.

There will be quite a few using statements that need to be added. But `Ctrl + .` should make quick work of that. However, there will be several types that you can, and should __not__, include using statements for.

Instead, you need to add a generic parameter to the method. It should be called `TProgram` and be a class

```csharp
public static async Task ExecuteTest<TProgram>(
        Func<DbCommand, Task> dbSetup, 
        Func<HttpClient, Task> test)
        where TProgram : class
{
    ...
}
```


You should then use this type instead of the `Program` type when you create the `WebApplicationFactory<T>`

```csharp
var app = new WebApplicationFactory<TProgram>()
```

You will also need a generic parameter for the `DbContext` to use.

```csharp
public static async Task ExecuteTest<TProgram, TDbContext>(
        Func<DbCommand, Task> dbSetup, 
        Func<HttpClient, Task> test)
        where TProgram : class
        where TDbContext : DbContext
{
    ...
}
```

You then need to update all places where the code uses `ProductsContext` to use `TDbContext` instead

```csharp
...
var dbDescriptor = services.First(x => x.ServiceType == typeof(TDbContext));
var optionsDescriptor = services.First(x => x.ServiceType == typeof(DbContextOptions<TDbContext>));
...
services.AddDbContext<TDbContext>(...);
...
var ctx = app.Services.GetRequiredService<TDbContext>();
```

Now, you don't want to hard code your products into every test. Instead, replace all the calls to `cmd.AddProduct()` with a single call to the `dbSetup` func

```csharp
...
cmd.Transaction = transaction.GetDbTransaction();

await dbSetup(cmd);

var client = app.CreateClient();
...
```


You also don't want the call to the API, and the assertions to be hard-coded. So, you can go ahead and replace the API call and the assertions with a call to the `test` func

```csharp
...
using (var conn = ctx.Database.GetDbConnection())
{
    ...

    await dbSetup(cmd);
    
    var client = app.CreateClient();
    
    await test(client);
}
...
```


__Important!__ As part of copying the code from the __WebDevWorkshop.Services.Products.Tests__ project, your IDE might have added a reference to it from the __WebDevWorkshop.Testing__ project. Make sure this is not the case. If there is a project reference from the __WebDevWorkshop.Testing__ project to the __WebDevWorkshop.Services.Products.Tests__ project, remove it. If left in place, it will break future code.

### Update the API test to use the TestHelper

Now that the `TestHelper` is done. You can go ahead and update the test to use it.

Open the __WebDevWorkshop.Services.Products.Tests__ project, and add a reference to the __WebDevWorkshop.Testing__ project.


You can then comment out the entire implementation block for the `GET_Returns_HTTP_200_and_all_products_marked_as_featured`, as this will require a lot of rework. However, you don't want to delete it yet, as you will still need some of the code.

```csharp
[Fact]
public async Task GET_Returns_HTTP_200_and_all_products_marked_as_featured()
// {
//     var app = new WebApplicationFactory<Program>()
//     ...
// }
```


Instead of this code, we can create a much simpler implementation that just calls the `TestHelper` class.

Start by removing the `async` keyword. The method doesn't need to be async anymore, as this is handled by the `TestHelper`

```csharp
[Fact]
public Task GET_Returns_HTTP_200_and_all_products_marked_as_featured()
...
```


Next, create a new implementation that simply calls the `TestHelper.ExecuteTest` method, passing in `Program` and `ProductsContext` as type parameters, and returns the returned `Task`.

```csharp
[Fact]
public Task GET_Returns_HTTP_200_and_all_products_marked_as_featured()
    => TestHelper.ExecuteTest<Program, ProductsContext>(
        dbSetup: async cmd => { },
        test: async client => { }
    );
```

You can then copy the `AddProduct` calls from the old implementation into the `dbSetup` callback

```csharp
dbSetup: async cmd => {
    await cmd.AddProduct("Product 1", "Description 1", 100m, true, "product1");
    await cmd.AddProduct("Product 2", "Description 2", 200m, true, "product2");
    await cmd.AddProduct("Product 3", "Description 3", 300m, true, "product3");
    await cmd.AddProduct("Product 4", "Description 4", 50m, false, "product4"); 
}
```

And then you can copy the actual test part into the `test` callback

```csharp
test: async client =>
{
    var response = await client.GetAsync("/api/products/featured");
    
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    
    var products = JArray.Parse(await response.Content.ReadAsStringAsync());
    
    Assert.Equal(3, products.Count);
    Assert.Contains(products, x => x.Value<string>("name") == "Product 1");
    Assert.Contains(products, x => x.Value<string>("name") == "Product 2");
    Assert.Contains(products, x => x.Value<string>("name") == "Product 3");
}
```

And finally, you can remove the old code that you commented out.

Turning the entire test into a much simpler thing

```csharp
public class FeaturedProductsEndpointTests
{
    [Fact]
    public Task GET_Returns_HTTP_200_and_all_products_marked_as_featured()
        => TestHelper.ExecuteTest<Program, ProductsContext>(
            dbSetup: async cmd => {
                await cmd.AddProduct("Product 1", "Description 1", 100m, true, "product1");
                await cmd.AddProduct("Product 2", "Description 2", 200m, true, "product2");
                await cmd.AddProduct("Product 3", "Description 3", 300m, true, "product3");
                await cmd.AddProduct("Product 4", "Description 4", 50m, false, "product4"); 
            },
            test: async client =>
            {
                var response = await client.GetAsync("/api/products/featured");
                
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                
                var products = JArray.Parse(await response.Content.ReadAsStringAsync());
                
                Assert.Equal(3, products.Count);
                Assert.Contains(products, x => x.Value<string>("name") == "Product 1");
                Assert.Contains(products, x => x.Value<string>("name") == "Product 2");
                Assert.Contains(products, x => x.Value<string>("name") == "Product 3");
            }
        );
}
```

### Verify that it works


Obviously, you "know" that this will work, as the code hasn't changed that much. But every developer knows that that is a fallacy. It needs to be verified...

So, go ahead and run the test, and verify that it is still green.

[<< Lab 6](../lab6/lab6.md) | [Lab 8 >>](../lab8/lab8.md)