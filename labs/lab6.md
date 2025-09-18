# Lab 6: Integration Testing the Products API

Now that you have an API that provides product information, it might be a good idea to set up some integration tests to make sure that it works as it should.

You could test the FastEndpoint endpoints using unit testing, or potentially integration testing. However, it ssems like a good idea to actually integration test the entire API using actual requests to it. This is a fairly simple thing to do, and ensures that the _whole_ application works as expected. It also allows you to test authentication, pipeline specific tweaks etc, as the test runs as a "real" HTTP request. Just testing the endpoints and down skips a fairly large part of the application.

Having tests that replicate the real world also allows us to test the format of the returned JSON. This makes it easy to check if future changes might cause problems.

## What are we doing?

As you are about to create a set of API integration tests, you will need a new project. In this case, it is a new xUnit Test Project that you should name __WebDevMasterClass.Services.Products.Tests__. Inside thise new project, you can create a class called __FeaturedProductsEndpointTests__.

Next, you need to add a reference to the __WebDevWorkshop.Services.Products__ project that you will be testing.

Create a test that is called __GET_Returns_HTTP_200_and_all_products_marked_as_featured__ and return Task, as it will be an asynchronous call to the API. Inside it, create a new `WebApplicationFactory<T>`, setting `T` to the `Program` class from the API project.

__Note:__ The `Program` class won't be available by default, as it is declared using top level statements in the __Program.cs__ file. To fix this, you need to declare a partial `Program` class at the bottom of the __Program.cs__ file.

Finally, use the `WebApplicationFactory<T>` instance to get hold of an `HttpClient`, make a __GET__ request to __/api/products/featured__ and then assert that the returned response has a 200 OK response code.

This test should go green if you were to run it. However, you probably want to have some data in the database to test against as well... One way to do this in a "safe" way, is to use an empty database that you fill with the required data for each test. And if you do this inside of a transaction, you can roll it back at the end of the test and have an empty database again. It also allows the tests to run in parallel without causing problems.

Unfortunately, in the current set up, the `ProductsContext` is registered as scoped, which makes it hard to manage the transaction. Any context you had access to in the test, would be a separate one from the one used during the request. However, as you are only going to make one request per test, using a singleton context would work just as well.

Use the `WithWebHostBuilder()` method on the `WebApplicationFactory<T>` to configure the web application.

The first configuration change that you need to do, is to modify the configuration so that you can get a different connectionstring for the tests. 

There are many ways to do this, but one is to create a new __appsettings.IntegrationTesting.json__ file in the API project, and then configure the test server to use the environment name __IntegrationTesting__.

__Note:__ This works because ASP.NET Core reads in any configuration defined in __appsettings.json__ and __appsettings.<ENVIRONMENT_NAME>.json during start up. So by setting the environment name, and providing a separate config for that environment, the tests will get their own config.

Inside the new appsettings, add a connectionstring called __WebDevWorkshop__ and point it towards a SQL Server using a database name that makes sense, for example __WebDevWorkshopTests__.

__Note:__ You cannot use the Aspire managed SQL Server in this case, as you will not start Aspire to run the tests. So, you will either need to use a SQL Server instance already installed on your machine, or simply spin up a new SQL Server container using a command like this `docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<password>" -p 1433:1433 --name sqltest -d mcr.microsoft.com/mssql/server:2025-latest`.

__Note:__ You don't need to create the database. The migration will automatically create it for you if it doesn't exist.

Once you have set up the new configuration and connectionstring, you need to update the DI registration for the `ProductsContext`. This is done by using the `ConfigureTestServices()` method on the web host builder. In the callback for this method, you need to find and remove, the service descriptors for the types `ProductsContext` and `DbContextOptions<ProductsContext>`. 

With the existing registrations removed, you can go ahead and add the `ProductsContext` back using the `AddDbContext<T>()` method. Making sure to configure it to be a singleton.

One last problem with this, is that EF by default uses retry policies. This does not work together with manually created transactions. So, you need to set the context's Execution Strategy to a `NonRetryingExecutionStrategy`.

Ok, with all of that mess out of the way, you can start updating the actual test. Right before the call to get the `HttpClient`, you need to get hold of the `ProductsContext` from the service collection. 

Next, you need to start a new transaction for the context, and put it in a `using` statement that makes sure to roll it back when the test ends. You also need to retrieve a `SqlConnection` from the context, and a `SqlCommand` from that.

__Note:__ As you know that you are using SQL Server, you can securely cast the `DbConnection` returned from the `ctx.Database.GetDbConnection()` method to `SqlConnection`.

You also need to remember to set the commands transaction to the one you just created.

__Note:__ Once again, you can simple cast it from `DbTransaction` to `SqlTransaction` as you know you are using SQL Server.

Once you have a database command, you can use it to create products in the database. However, as a lot of tests will likely create products, it is best to make that code reusable. To do so, create a new static class called __SqlCommandExtensions__.

__Note:__ You probably want to put that class in a separate directory like __Data__ or something, to keep it our of the way of the tests...

In that class, create an extension method called __AddProduct__ that extends `SqlCommand`, takes all the values required for a product as parameters, and asyncronously returns the newly created products id.

Inside the extension method, add code to add a `Product` to the database using the provided inputs. And then return the generated id.

Now that you have that extension method, go back to the test and use it to add a few products to the database, using the `SqlCommand` instance you got hold of before.

With the data in place, make sure you the client creation and test code is inside of the transactions `using` scope.

Now that you have data in your test as well, you should probably add a few more asserts to verify the returned data.

Use `JArray.Parse()` to parse the returned data, and then use the `JArray` to verify that the returned data is what it is supposed to be.

__Note:__ Remember that the property names will be translated from PascalCasing to camelCasing...

This is everything you need to do. Except...the migrations are only run during start up if you are in development. Now that you have changed the environment name from `Development` to `IntegrationTesting`, this is no longer the case, which is actually a good thing. You do not want to run the migrations on every test. Instead, you want to run them before every test run. 

There are several ways to run the migrations on every test run in xUnit. in this case, you will use a `XunitTestFramework` class.

Go ahead and create a new class called __TestRunStart__. Replace the contents of the file with the code in [this file](../resources/testrunstart.md).

This adds an assembly level attribute that tells xUnit that you want to create an instance of this class whenever a test run is started. Inside the constructor of the class, it gets hold of the configuration, creates a `ProductsContext` and runs the migrations.

__Warning:__ If this code throws an exception, xUnit will swallow this exception and just carry on. So, if you suspect that the database hasn't been migrated for your tests, you need to make sure your migrations are actually run, by adding a breakpoint in there.

You should be able to run your test now. And it should go green if you have done everything correctly.

## Steps (for Visual Studio)

### Create a new test project

The first step is to add a new __xUnit Test Project__ to your solution. Calling it __WebDevWorkshop.Services.Products.Tests__ makes sense.

The new project comes with a __UnitTest1.cs__ file. Rename it to __FeaturedProductsEndpointTests.cs__ and update the class name inside it to `FeaturedProductsEndpointTests`.

With the test project in place, add a reference to the __Microsoft.AspNetCore.Mvc.Testing__ NuGet package. This package contains the in-memory webserver that you will use to run the integration tests.

You also need to add a reference to the project to test. In this case the __WebDevWorkshop.Services.Products__ project.

### Definings the first test

Open the __FeaturedProductsEndpointTests.cs__ file, and rename the __Test1__ test to __GET_Returns_HTTP_200_and_all_products_marked_as_featured__.

__Note:__ Yes, that is a long name, but it is also very descriptive, which is good.

It also needs to be updated to asyncronously return `Task`, as the test will be asyncronous.

```csharp
[Fact]
public async Task GET_Returns_HTTP_200_and_all_products_marked_as_featured()
{
}
```

Inside the test, you need to create a `WebApplicationFactory<T>`, where `T` is the `Program` class for your tested project. 

Unfortunately, the `Program` class isn't available outside of the __WebDevWorkshop.Services.Products__ project. The reason for that, is that the class becomes internal when you use top level statements. To fix this, you need to open the __Program.cs__ file in the __WebDevWorkshop.Services.Products__ project, and add the following code to the bottom of the file

```csharp
public partial class Program { }
```

This tells the compiler to make the `Program` class public.

Now that the `Program` class is public, you can go back to the test and create an instance of the `WebApplicationFactory<T>`

```csharp
[Fact]
public async Task GET_Returns_HTTP_200_and_all_products_marked_as_featured()
{
    var app = new WebApplicationFactory<Program>();
}
```

Once you have an instance of `WebApplicationFactory<T>`, you can easily create an `HttpClient` from it, by calling `CreateClient()`. And with that client, you can then make a __GET__ request to __/api/products/featured__.

```csharp
var client = app.CreateClient();
var response = await client.GetAsync("/api/products/featured");
```

Finally, you need to assert that the API returns an HTTP 200 status code.

```csharp
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```

### Setting up test data infrastructure

The test kind of works now. However, the `ProductsContext` would potentially crash depending on the situation. If you hadn't started the Aspire project before the tests, the SQL Server wouldn't be online, and thus the test would fail as it couldn't connect to the database. If you had started the Aspire project, and the SQL Server was up and running, the test would get whatever data was in the database. And since you wouldn't really know what was in there, you would not be able to write your test properly.

To fix this, you will need to figure out a way to handle test data. In this workshop, the suggested approach is this.

Create another "permanent" SQL Server instance using Docker. Run the migrations before every test run (not every test) to get the database tables in place. And then, inside each test, add the data required for that specific test. And to make sure that the test data doesn't cause problems in other tests, you will wrap a database transaction around the test. This will make sure that any data that is added to it, is rolled back at the end, leaving the database in a prinstine condition.

There are however a few things that needs to be set up for this to work.

First of all, you need to set up a new SQL Server Docker container. This is quite easily done by running the following command

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<password>" -p 14330:1433 --name sqltest -d mcr.microsoft.com/mssql/server:2022-latest
```

__Note:__ `<password>` needs to be replaced with a password that is complex enough for SQL Server to accept it. __Password123__ is...

__Note:__ The server is set up to run on port __14330__ instead of the default to make sure it doesn't collide with any existing instance. It also does not use any volumes to store the data, as it doesn't matter if the database disappears. The tests will figure that out.

Next, you need to set up some configuration to get hold of the connectionstring. The "easiest" way to do this, is to simply change the applications environment name to __IntegrationTesting__. This will cause ASP.NET Core to read in __appsettings.json__ as well as __appsettings.IntegrationTesting.json__ during start up. Allowing us to have a separate config file for integration tests.

In the test, add a call to `WithWebHostBuilder()` on the `WebApplicationFactory<Program>` instance.

```csharp
var app = new WebApplicationFactory<Program>()
                    .WithWebHostBuilder(builder =>
                    {
                        
                    });
```

This allows you to reconfigure the applications host. Doing things like changing the environment name, modify services etc.

In this case, you should go ahead and change the environment name by calling the following method

```csharp
builder.UseEnvironment("IntegrationTesting");
```

With that in place, you can go ahead and create a copy of the __appsettings.json__ file in the __WebDevWorkshop.Services.Products__, and rename it to __appsettings.IntegrationTesting.json__.

Inside the new file, add a connectionstring called __WebDevWorkshop__ (the name used for the database resource in the AppModel).

```json
{
  "Logging": {
    ...
  },
  "ConnectionStrings": {
    "WebDevWorkshop": "Server=localhost,14330;User ID=sa;Password=Password123;TrustServerCertificate=true;Initial Catalog=WebDevWorkshopTests"
  }
}
```

Ok, now you have the configuration needed for the `ProductsContext`. However, there are a few technical problems still.

First of all, the `ProductsContext` is registered as __Scoped__ by default. This causes problems when you want to set up a transaction. Why? Well, if you tried to get hold of a `ProductsContext` in the test, it would be a different context to the one used by the application for the actual request. Thus, your transaction, and data, would end up not affecting the response.

To solve this, you need to re-register the context as a __Singleton__.

__Note:__ As you only make one request per test, there is really no difference between a __Scoped__ and __Singleton__ registration. Except that you can add your transaction to it...

To modify any services in the `WebApplicationFactory<Program>`, you can call `ConfigureTestServices()` on the web host builder.

```csharp
var app = new WebApplicationFactory<Program>()
                    .WithWebHostBuilder(builder =>
                    {
                        ...
                        builder.ConfigureTestServices(services =>
                        {
                            
                        });
                    });
```

Inside this callback, you can modify the `IServiceCollection` as much as you want. Often times you will use it to add faked services. But in this case, you want to replace the `ProductsContext` registration to make it __Singleton__.

To do this, you first need to find the so called `ServiceDescriptor` that defines the registration of a service. However, you not only need to find the registration of the `ProductsContext`, you also need to find the `DbContextOptions<ProductsContext>`, which is added in parallel to the `ProductsContext`.

```csharp
builder.ConfigureTestServices(services =>
{
    var dbDescriptor = services.First(x => x.ServiceType == typeof(ProductsContext));
    var optionsDescriptor = services.First(x => x.ServiceType == typeof(DbContextOptions<ProductsContext>));
});
```

Once you have the `ServiceDescriptor` instances, you can go ahead and remove them from the service collection

```csharp
builder.ConfigureTestServices(services =>
{
    ...
    services.Remove(dbDescriptor);
    services.Remove(optionsDescriptor);
});
```

And then you need to re-register the `ProductsContext` as a __Singleton__

```csharp
builder.ConfigureTestServices(services =>
{
    ...
    services.AddDbContext<ProductsContext>((services, options) => {
        var config = services.GetRequiredService<IConfiguration>();
        options.UseSqlServer(config.GetConnectionString("WebDevWorkshop"));
    }, ServiceLifetime.Singleton);
});
```

Ok, so now you have a __Singleton__ `ProductsContext`to use. However, there is one small issue still... By default, EF Core uses retry policies to make your application more resilient. The problem is that you cannot use manually created transaction together with retry policies. So, you will have to disable that. And they way you do that, is by replacing the defaul `ExecutionStrategy` with one called `NonRetryingExecutionStrategy`

```csharp
options.UseSqlServer(config.GetConnectionString("WebDevWorkshop"),
    options =>
    {
        options.ExecutionStrategy(x => new NonRetryingExecutionStrategy(x));
    }
);
```

Now that you have a correctly configured `ProductsContext`, you can go back to focusing on the actual test data.

The first thing you need to do to add the test data, is to get hold of the `ProductsContext` from the DI... So, after the configuration of the web host, add the following code to get hold of it

```csharp
var ctx = app.Services.GetRequiredService<ProductsContext>();
```

Once you have the `ProductContext` instance, you can start a new transaction by calling `ctx.Database.BeginTransaction()`. This returns an `IDbContextTransaction`, which happens to implement `IDisposable`. So, you can add it to a `using` statement. 

Add the the following code right before the creation of the `HttpClient`

```csharp
var ctx = app.Services.GetRequiredService<ProductsContext>();
using (var transaction = ctx.Database.BeginTransaction())
{
    
}

var client = app.CreateClient();
```

From the `Database`, you can also get hold of a `DbConnection` by calling `GetDbConnection()`. Once again, it implements `IDisposable`, so you might as well add a `using` statement to it.

```csharp
var ctx = app.Services.GetRequiredService<ProductsContext>();
using (var transaction = ctx.Database.BeginTransaction())
using (var conn = ctx.Database.GetDbConnection())
{
    
}
```

Using the `DbConnection`, you can then create a `DbCommand` and assign the transaction to it.

```csharp
using (var transaction = ctx.Database.BeginTransaction())
using (var conn = ctx.Database.GetDbConnection())
{
    var cmd = conn.CreateCommand();
    cmd.Transaction = transaction.GetDbTransaction();
}
```

Now, you could start adding products to the database right here, using the `DbCommand`. However, seeing that it is likely that you willo do this in many places, in many tests, you might as well make it reusable. And the easiest way to do that, is to use an extension method.

Create a new directory called __Data__, to keep this stuff away from the actual tests.

In the __Data__ directory, add a new class called __DbCommandExtensions__. Make the class static so that you can add extension methods to it.

```csharp
public static class DbCommandExtensions
{
    
}
```

Inside this class, add an extension method called __AddProduct__. It should extend `DbCommand`, and take all the properties required for a `Product` as parameters. It should also return the `int` id of the created row. And as it talks to a database, it needs to be async.

```csharp
public static async Task<int> AddProduct(this DbCommand cmd,
                                            string name,
                                            string description,
                                            decimal price,
                                            bool isFeatured,
                                            string imageName)
{
}
```

The implementation is good ol' ADO.NET code...

```csharp
cmd.CommandText = "INSERT INTO Products (Name, Description, Price, IsFeatured, ThumbnailUrl, ImageUrl) " +
                          "VALUES (@Name, @Description, @Price, @IsFeatured, @ThumbnailUrl, @ImageUrl); " +
                          "SELECT SCOPE_IDENTITY();";

cmd.Parameters.Add(new SqlParameter("@Name", name));
cmd.Parameters.Add(new SqlParameter("Description", description));
cmd.Parameters.Add(new SqlParameter("Price", price));
cmd.Parameters.Add(new SqlParameter("IsFeatured", isFeatured));
cmd.Parameters.Add(new SqlParameter("ThumbnailUrl", $"{imageName}_thumbnail.jpg"));
cmd.Parameters.Add(new SqlParameter("ImageUrl", $"{imageName}.jpg"));

var ret = Convert.ToInt32(await cmd.ExecuteScalarAsync());

cmd.Parameters.Clear();

return ret;
```

__Note:__ Yes, it adds `SqlParameter` instances... But you already know that it is going to use SQL Server, so that should not cause any problems. And the syntax is nicer with those, than trying to use a more generic type...

With that extension method in place, you can go back to the test and add some products to the database

```csharp
var ctx = app.Services.GetRequiredService<ProductsContext>();
using (var transaction = ctx.Database.BeginTransaction())
using (var conn = ctx.Database.GetDbConnection())
{
    var cmd = conn.CreateCommand();
    cmd.Transaction = transaction.GetDbTransaction();

    await cmd.AddProduct("Product 1", "Description 1", 100m, true, "product1");
    await cmd.AddProduct("Product 2", "Description 2", 200m, true, "product2");
    await cmd.AddProduct("Product 3", "Description 3", 300m, true, "product3");
    await cmd.AddProduct("Product 4", "Description 4", 50m, false, "product4");
}
```

Currently, the actual test functionality is outside of the `using` block. So, go ahead and move the `HttpClient` creation, usage and assertion inside the `using` block

```csharp
var ctx = app.Services.GetRequiredService<ProductsContext>();
using (var transaction = ctx.Database.BeginTransaction())
using (var conn = ctx.Database.GetDbConnection())
{
    ...

    var client = app.CreateClient();
    var response = await client.GetAsync("/api/products/featured");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

Now that you have some data in the database, it's possible to add some more assertions to make sure that the returned data is what you expect.

When doing API integration testing, it is a good idea to verify the data "as-is". That is, verify the actual JSON in this case, instead of deserializing it to some object. By doing ti like this, you getter better backwards compatibility checks.

So, once you got your response from the server, and you know the repsonse is OK, you can go ahead and parse it using `JArray.Parse()`, as you know it is going to return an array.

__Note:__ `JArray.Parse` comes out of Json.NET. It is a bit easier to parse raw JSON using that, compared to using the classes in `System.Text.Json`.

```csharp
Assert.Equal(HttpStatusCode.OK, response.StatusCode);

var products = JArray.Parse(await response.Content.ReadAsStringAsync());
```

The actual data isn't that important for this workshop, but doing the assertions looks something like this

````csharp
var products = JArray.Parse(await response.Content.ReadAsStringAsync());
            
Assert.Equal(3, products.Count);
Assert.Contains(products, x => x.Value<string>("name") == "Product 1");
Assert.Contains(products, x => x.Value<string>("name") == "Product 2");
Assert.Contains(products, x => x.Value<string>("name") == "Product 3");
````

### Running the migrations

As the test stands right now, it would actually fail. The reason being that there is no database to talk to. There is a SQL Server instance, but no database. 

To set up the database, you need to run the migrations. However, the migrations only run if you are en development mode. And you are not anymore, as you have changed the environment name from the default __Development__ to __IntegrationTesting__.

To fix this, you need to tell xUnit to run the migrations for you. And you also want to make sure that they only run once per test run, not per test.

One way to do this, is to create a class that inherits from `XunitTestFramework`, and then tell xUnit that this class should be instantiated at start up using an assembly level attribute of the type `TestFramework`.

Create a directory called __Infrastructure__ and add a class called __TestRunStart__ inside it. 

The implementation of this class should look like this

```csharp
public class TestRunStart : XunitTestFramework
{
    public TestRunStart(IMessageSink messageSink) : base(messageSink)
    {
        var config = new ConfigurationManager()
                        .AddJsonFile("appsettings.IntegrationTesting.json")
                        .Build();

        var options = new DbContextOptionsBuilder<ProductsContext>()
                            .UseSqlServer(config.GetConnectionString("WebDevWorkshop"));

        var dbContext = new ProductsContext(options.Options);

        dbContext.Database.Migrate();
    }
}
```

As you can see, it inherits from `XunitTestFramework`, and has a bunch of code in the constructor. The code in the constructor gets hold of the __appsettings.IntegrationTesting.json__, creates a `ProductsContext` using the connectionstring in the config, and then runs the migrations.

The only thing left to do, is to tell xUnit that this file extsis, using an assembly level `TestFramework` attribute that points to this class

```csharp
...
using Xunit.Sdk;

[assembly: TestFramework(
    "WebDevWorkshop.Services.Products.Tests.Infrastructure.TestRunStart",
    "WebDevWorkshop.Services.Products.Tests"
)]

namespace WebDevWorkshop.Services.Products.Tests.Infrastructure;
...
```

### Verify that it works

The last step is to verify that the test actually works. So, go ahead and pull up the Test Explorer and run the test.

__Note:__ You might need to re-build the solution to get the Test Explorer to see the test

The test should now go green! If it doesn't try debugging the test to see what has failed.

[<< Lab 5](./lab5.md) | [Lab 7 >>](./lab7.md)