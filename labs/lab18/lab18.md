# Lab 18: Testing gRPC Services

Now that you have a new service, it might be worth testing that as well. And even though this is implemented using gRPC instead of good old HTTP and JSON, it is still possible to test it in-memory in the same way you have tested the other endpoints. However, there are some tweaks to be made for it to work.

## Steps (for Visual Studio)

###  Creating the testing project

The first step is, as "always", to create a new __xUnit Test__ project. And in this case, calling it __WebDevWorkshop.Services.Orders.Tests__ seems appropriate.

And once again, the __UnitTest1.cs__ doesn't really make any sense. So, go ahead and rename it to __OrdersServiceTests.cs__, and the class inside it to __OrdersServiceTests__.

The test needs to be renamed as well. So, rename it to __Adds_order_to_db__, and make it return a `Task`

```csharp
public class OrdersServiceTests
{
    [Fact]
    public Task Adds_order_to_db()
    {
        return Task.CompletedTask;
    }
}
```

Once again, you will use the `TestHelper` class. Or at least parts of it. So, add a reference to the __WebDevWorkshop.Testing__ project.

### Creating a TestHelper method

Open the __TestHelper.cs__ file in the __WebDevWorkshop.Testing__ project.

As you are going to be testing a gRPC-based service, you need to add a NuGet package called __Grpc.Net.Client__.

__Note:__ This is different from the __Grpc.AspNetCore__ package you use in the actual service, but that's because you only need access to a class used as a base class when the __Grpc.AspNetCore__ package creates a gRPC client. (It will hopefully make more sense in a while...)

Now, as the test is testing a gRPC-client, you can't really re-use any of the `ExecuteTest` methods you created earlier. Instead, you will need to create a new one.


So, at the bottom of the `TestHelper` class, add a new method with a signature that looks like this

```csharp
public static async Task ExecuteTest<TProgram, TDbContext, TGrpcService>(
        Func<TGrpcService, Task> test,
        Func<DbCommand, Task>? dbConfig = null,
        Func<DbCommand, Task>? validateDb = null
    )
        where TProgram : class
        where TDbContext : DbContext
        where TGrpcService : ClientBase
{
    
}
```

As you can see, it is very similar to the one you created when you tested the products service. However, as this test needs to use a gRPC client to talk to the service, it has an extra type parameter called `TGrpcService` that will be the type of the gRPC client.

It also has a new `validateDb` parameter. This is a callback that will allow you to verify that the database looks as expected after the call to the service has completed.

The implementation will be very similar as well. It starts out with creating, and configuring a `WebApplicationFactory<T>`

```csharp
var app = new WebApplicationFactory<TProgram>()
    .WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("IntegrationTesting");

        builder.ConfigureTestServices(services =>
        {
            var dbDescriptor = services.First(x => x.ServiceType == typeof(TDbContext));
            var optionsDescriptor = services.First(x => x.ServiceType == typeof(DbContextOptions<TDbContext>));

            services.Remove(dbDescriptor);
            services.Remove(optionsDescriptor);

            var config = services.BuildServiceProvider().GetRequiredService<IConfiguration>();

            services.AddDbContext<TDbContext>((services, options) =>
            {
                var config = services.GetRequiredService<IConfiguration>();
                options.UseSqlServer(config.GetConnectionString("WebDevWorkshopOrders"), options =>
                {
                    options.ExecutionStrategy(x => new NonRetryingExecutionStrategy(x));
                });
            }, ServiceLifetime.Singleton);
        });
    });
```

To be honest, it is pretty much identical. So, it would probably be a good idea to refactor it to get rid of the code duplication. But let's ignore that, as it is just a workshop lab, not production code.

The second part of the method is actually almost identical as well...

```csharp
using (var services = app.Services.CreateScope())
{
    var ctx = services.ServiceProvider.GetRequiredService<TDbContext>();
    using (var transaction = ctx.Database.BeginTransaction())
    {
        var conn = ctx.Database.GetDbConnection();
        var cmd = conn.CreateCommand();
        cmd.Transaction = transaction.GetDbTransaction();

        if (dbConfig != null)
            await dbConfig(cmd);

        // Execute the test...

        if (validateDb != null)
            await validateDb(cmd);
    }
}
```

However, as you can see, there are two differences. First of all, there is a comment that says "Execute the test...", right where the previous method created an `HttpClient` and called the `test` callback. But, this is a gRPC test, not an `HttpClient` test, so the code needs to be a bit different in that area.

Secondly, there is a call to the new `validateDb` callback at the end. This allows the test to verify the contents of the database after the call to the service. As mentiond before.

But let's focus on the test execution now. And to do that, you need to create a gRPC client to talk to the service.

The way you create a gRPC client is to start off by creating a gRPC channel. This can be done using a call to the static `GrpcChannel.ForAddress()` passing in the address to the server.

In this case, the server is the in-memory `WebApplicationFactory<T>` instance, which actually do expose an address, even if it is in-memory. It's exposed using the `Server.BaseAddress` property. So, to create a `GrpcChannel`instance, you just write

```csharp
// Execute the test...
var channel = GrpcChannel.ForAddress(app.Server.BaseAddress);
```

The problem is that this doesn't actually work. You can't call the in-memory server using that address unfortunately.

When you created the `HttpClient` in the other helper methods, that client actually has a custom `HttpHanlder` inside it, that takes your call and converts it in to a call being made without the actual HTTP part. In this case, we need to do that manually.

So, in the code, right before you create the `GrpcChannel`, you need to create a new `GrpcChannelOptions`, and set the `HttpHandler` property, to a `HttpHandler` created by calling the app's `Server.CreateHandler()` method

```csharp
// Execute the test...
var options = new GrpcChannelOptions {
    HttpHandler = app.Server.CreateHandler()
};
var channel = GrpcChannel.ForAddress(app.Server.BaseAddress);
```

You then need to pass that `GrpcChannelOptions` instance as a second parameter to the `GrpcChannel.ForAddress()` method.

```csharp
// Execute the test...
var options = new GrpcChannelOptions {
    HttpHandler = app.Server.CreateHandler()
};
var channel = GrpcChannel.ForAddress(app.Server.BaseAddress, options);
```

Once you have the gRPC channel, you can create the gRPC client. For this, you will actually need to use some reflection.

Because you have required that the `TGrpcService` to be of the type `ClientBase`, you can safely assume that it will have a constructor that takes a single `GrpcChannel` parameter. This in turn let's you safely create a new instance using the `Activator.CreateInstance()` method like this

```csharp
var channel = GrpcChannel.ForAddress(app.Server.BaseAddress, options);
var client = (TGrpcService)Activator.CreateInstance(
    typeof(TGrpcService), 
    channel
)!;
```

And then, the last part is to simply call the `test` callback

```csharp
var channel = GrpcChannel.ForAddress(...);

var client = (TGrpcService)Activator.CreateInstance(...)!;

await test(client);
```

Now you are ready to start writing the first test.

### Creating the first gRPC-based test

Open the __WebDevWorkshop.Services.Orders.Tests__ project, and add a reference to the __WebDevWorkshop.Services.Orders__ project. This is needed to access the `Program` class. Unfortunately, it will also cause some problems in a minute. But ignore that for now...

By adding that reference, you get access to the classes used to create the server-side part. It doesn't give you a client that you can use to talk to the service. And the reason for that is that the __Protobuf__ element in that project was defined as `GrpcServices="Server"`.

To fix that, the test project will need to create its own gRPC client. And to do that, you need the __Grpc.AspNetCore__ NuGet package. So, go ahead and add that to the __WebDevWorkshop.Services.Orders.Tests__ project.

Now, to be able to use that package to create a gRPC client, you need have access to the proto file. Unfortunately, that is defined in the __WebDevWorkshop.Services.Orders__ project.

Now, you could go and create a copy of the file. But that is definitely not recommended, as they might go out of sync. Instead, right-click the __WebDevWorkshop.Services.Orders.Tests__ project in the Solution Explorer and select __Add__ > __Existing Item__. In the window that opens, located the __WebDevWorkshop.Services.Orders\Protos\orders.proto__ file. But do __NOT__ click __Add__. Instead, click on the little arrow to the right and then __Add As Link__. This will add a link to the file, instead of a copy.

Open the __WebDevWorkshop.Services.Orders.Tests.csproj__ file, and make sure that the file you just linked, is liked as a __&lt;Protobuf /&gt;__ element that looks like this

```xml
<ItemGroup>
    <Protobuf Include="..\WebDevWorkshop.Services.Orders\Protos\orders.proto">
        <Link>orders.proto</Link>
    </Protobuf>
</ItemGroup>
```

This should cause the __Grpc.AspNetCore__ NuGet package to generate a gRPC client for you as soon as you build your project. So...go ahead and build the project by pressing __Ctrl + Shift + B__ or whatever way you normally build a project.

If you now open the __Error List__, you will see a ton of warnings saying that a bunch of classes have conflicting implementations. The reason for this is that all of the gRPC message classes are being generated by both the __WebDevWorkshop.Services.Orders__ and the __WebDevWorkshop.Services.Orders.Tests__ project now. 

The best way to sort this out, is to alias the reference from the test project to the service project.

Open the __WebDevWorkshop.Services.Orders.Tests.csproj__ file, and update the __&lt;ProjectReference /&gt;__ element that points to the __WebDevWorkshop.Services.Orders.csproj__ file to look like this

```xml
<ProjectReference Include="..\WebDevWorkshop.Services.Orders\WebDevWorkshop.Services.Orders.csproj">
    <Aliases>SERVER</Aliases>
</ProjectReference>
```

This is a bit weird, but it will cause the compiler to ignore anything inside that project unless you explicitly tell it to include parts of it.

__Note:__ You might need to reload the project to get the alias to work in Visual Studio.

If you try to re-build the project now, you will see that all the warnings go away. Apart for a maybe a couple of nullability warnings. 

Open the __OrdersServiceTests.cs__ file, and replace the implementation of the `Adds_order_to_db()` method with a call to the `TestHelper.ExecuteTest` that you just created

```csharp
[Fact]
public Task Adds_order_to_db()
    => TestHelper.ExecuteTest<Program, OrdersContext, gRPC.OrdersService.OrdersServiceClient>(
        test: async client =>
        {

        });
```

Now, the problem is, as it has been several times before, that you can't reference the `Program` class. But you know how to fix that. Just open the __Program.cs__ file in the __WebDevWorkshop.Services.Orders__ project, and add a `public`, `partial` class called __Program__ at the bottom of the file

__Warning:__ If you do not get an error from the usage of the `Program` class, it probably because VS has added a using statement for the `Microsoft.VisualStudio.TestPlatform.TestHost` namespace. This namespace includes a `Program` class as well, but it is not the one you want...

```csharp
...
app.Run();

public partial class Program { }
```

And then go back to the __OrdersServiceTests.cs__ file. 

Wait...that didn't help! The `Program` reference is still incorrect... And the reason for that, is the __SERVER__ alias you put in the csproj-file. As this causes the compiler to not load anything from that project, it obviously can't find the `Program` file, even if there is a public one now.

The solution is to explicitly add a reference to that project, and then use that reference to reference the `Program` class.

So, at the top of the file, add the following row of code

```csharp
extern alias SERVER;
...
```

This creates an alias you can use to reference things inside the project aliased as __SERVER__. And using that alias, you can reference the `Program` file by writing

```csharp
TestHelper.ExecuteTest<SERVER::Program, ...>(...);
```

Now, you could do the same thing for the `OrdersContext`. But after some time, this might look quite ugly if you need to reference a lot of classes from the aliased project. 

A better solution is to do a named using statement, using the alias.

```csharp
extern alias SERVER;
using OrdersService = SERVER::WebDevWorkshop.Services.Orders;
...
```

Now, you can the reference anything inside the `WebDevWorkshop.Services.Orders` namespace, or below it, in the aliased project, by simply prefixing it with `OrdersService`.

```csharp
TestHelper.ExecuteTest<..., OrdersService.Data.OrdersContext, ...>(...);
```

Your code should be ok now! And you should be able to focus on writing the actual test.

To be able to call the `AddOrder` method, you will need an `AddOrderRequest` instance. So, go ahead and create one of those using this code

```csharp
var request = new AddOrderRequest
{
    DeliveryAddress = new Address
    {
        Name = "Chris Klug",
        Street1 = "Teststreet 1",
        Street2 = "",
        PostalCode = "12345",
        City = "Stockholm",
        Country = "Sweden"
    },
    BillingAddress = new Address
    {
        Name = "John Doe",
        Street1 = "Somestreet 1",
        Street2 = "",
        PostalCode = "56789",
        City = "Whoville",
        Country = "Denmark"
    }
};
```

__Warning:__ When adding the using statement for the `AddOrderRequest`, make sure you add `using WebDevWorkshop.Services.Orders.gRPC;` to add the local class, instead of the one in the aliased project.

You also need to add an `OrderItem` to the order.

```csharp
var request = new AddOrderRequest
{
    ...
}

request.Items.Add(new OrderItem
{
    Name = "My Product",
    Price = 123.5f,
    Quantity = 2
});
```

Once you have a complete `AddOrderRequest`, you can go ahead and call the service! 

The the source generator actually generates both a synchronous and an asynchronous version of each method in the proto-file. But you really should try to use the asynchronous one if you can. Don't do potentially long-running calls uing synchronous methods if you can avoid it.

```csharp
request.Items.Add(...);

var response = await client.AddOrderAsync(request);
```

And now that you have the response, you can go ahead and assert some things about it. Like, is it not null? Is the `Success` property `true`? And is the `OrderId` set?

```csharp
Assert.NotNull(response);
Assert.True(response.Success);
Assert.NotNull(response.OrderId);
```

This verifies that the returned object looks ok. But it doesn't verify what the test says it is verifying. That the order is added to the database.

For that, you need to use that `validateDb` callback that was added to this version of the `TestHelper.ExeuteTest()` method.

```csharp
TestHelper.ExecuteTest<...>(
    test: async client => { ... },
    validateDb: async cmd => {
        
    });
```

The assertions to be done in there are pretty mundane, but requires quite a bit of code. But as it isn't important to the lab as such, the simplest way to implement it is to simply copy this code

```csharp
int id;
cmd.CommandText = "SELECT * FROM Orders";
using (var reader = await cmd.ExecuteReaderAsync())
{
    Assert.True(reader.Read());
    id = (int)reader["Id"];
    Assert.Equal(247, (decimal)reader["Total"]);
    Assert.False(reader.Read());
}

cmd.CommandText = "SELECT * FROM OrderItems WHERE OrderId = " + id;
using (var reader = await cmd.ExecuteReaderAsync())
{
    Assert.True(reader.Read());
    Assert.Equal("My Product", (string)reader["Name"]);
    Assert.Equal(123.5m, (decimal)reader["Price"]);
    Assert.Equal(2, (int)reader["Quantity"]);
    Assert.False(reader.Read());
}

cmd.CommandText = "SELECT * FROM Addresses WHERE AddressType = 'Delivery' AND OrderId = " + id;
using (var reader = await cmd.ExecuteReaderAsync())
{
    Assert.True(reader.Read());
    Assert.Equal("Chris Klug", (string)reader["Name"]);
    Assert.Equal("Teststreet 1", (string)reader["Street1"]);
    Assert.Equal("", (string)reader["Street2"]);
    Assert.Equal("12345", (string)reader["PostalCode"]);
    Assert.Equal("Stockholm", (string)reader["City"]);
    Assert.Equal("Sweden", (string)reader["Country"]);
    Assert.False(reader.Read());
}

cmd.CommandText = "SELECT * FROM Addresses WHERE AddressType = 'Billing' AND OrderId = " + id;
using (var reader = await cmd.ExecuteReaderAsync())
{
    Assert.True(reader.Read());
    Assert.Equal("John Doe", (string)reader["Name"]);
    Assert.Equal("Somestreet 1", (string)reader["Street1"]);
    Assert.Equal("", (string)reader["Street2"]);
    Assert.Equal("56789", (string)reader["PostalCode"]);
    Assert.Equal("Whoville", (string)reader["City"]);
    Assert.Equal("Denmark", (string)reader["Country"]);
    Assert.False(reader.Read());
}
```

__Note:__ You would probably want to clean this up using extension methods or something like that. But it is fine like this for this lab...

### Verify that it works

Open the Test Explorer and run the new test!

Unfortunately, that results in an error that says __ConnectionString missing__...

That's because you created a new database for the orders service. And a new connectionstring called __WebDevWorkshopOrders__. But you haven't added it to the __appsettings.IntegrationTesting.json__ file. Actually, you haven't even added that file. 

So, go ahead and copy the __appsettings.IntegrationTesting.json__ from the __WebDevWorkshop.Services.Products__ project, and add it to the __WebDevWorkshop.Services.Orders__. Then rename the connectionstring inside it to __WebDevWorkshopOrders__, and change the `Initial Catalog` to __WebDevWorkshop.Orders__.

```json
"ConnectionStrings": {
    "WebDevWorkshopOrders": "Server=localhost,14330;User ID=sa;Password=Password123;TrustServerCertificate=true;Initial Catalog=WebDevWorkshop.Orders"
  }
```

Now, try running the test again, using the Test Explorer.

Unfortunately this will result in another error! After a bit of time at least.. And this time it says __Cannot open database "WebDevWorkshop.Orders" requested by the login.__. And that's because there is no database. You haven't run the migrations.

And just as the last time you needed to run the migrations, in the __WebDevWorkshop.Services.Products.Tests__ project, you need a `TestRunStart` class. 

__Note:__ It would be nice if you could put that in the common __WebDevWorkshop.Testing__ project. However, as it uses assembly level attribibutes, this won't work.

However, considering how similar they are, just go ahead and copy the whole __Infrastructure__ directory from the __WebDevWorkshop.Services.Products.Tests__ project. Then open the __TestRunStart.cs__ file and make the following changes.

Add a __SERVER__ alias at the top

```csharp
extern alias SERVER;
```

Update the using statement for the `WebDevWorkshop.Services.Products.Data` namespace to say `SERVER::WebDevWorkshop.Services.Orders.Data`.

Change the namespace to be `WebDevWorkshop.Services.Orders.Tests.Infrastructure`.

Replace the `ProductsContext` with `OrdersContext`.

Update the connectionstring name to __WebDevWorkshopOrders__

And finally, update the `TestFramework` to point to the correct assembly and namespace

```csharp
extern alias SERVER;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SERVER::WebDevWorkshop.Services.Orders.Data;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework(
    "WebDevWorkshop.Services.Orders.Tests.Infrastructure.TestRunStart",
    "WebDevWorkshop.Services.Orders.Tests"
)]

namespace WebDevWorkshop.Services.Orders.Tests.Infrastructure;

public class TestRunStart : XunitTestFramework
{
    public TestRunStart(IMessageSink messageSink) : base(messageSink)
    {
        var config = new ConfigurationManager()
            .AddJsonFile("appsettings.IntegrationTesting.json")
            .Build();

        var options = new DbContextOptionsBuilder<OrdersContext>()
            .UseSqlServer(config.GetConnectionString("WebDevWorkshopOrders"));

        var dbContext = new OrdersContext(options.Options);

        dbContext.Database.Migrate();
    }
}
```

Now, try to run the test again. It should go green!

[<< Lab 17](../lab17/lab17.md) | [Lab 19 >>](../lab19/lab19.md)