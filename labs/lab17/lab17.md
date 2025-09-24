# Lab 17: Creating a gRPC-based Orders Service

The website needs to support taking orders as well. And since it is a service-based architecture, that means that you need another service to handle that. And because of Reasons™️, upper management has decided that gRPC would be the best option for this.

__Note:__ gRPC is actually not a bad option for this at all, as it is a service that can be easily provided using an RPC-style API.

## Steps (for Visual Studio)

###  Creating a gRPC service

The first step is to create a new __ASP.NET Core gRPC Service__ project called __WebDevWorkshop.Services.Orders__. And as usual, don't forget to tick that "_Enlist in .NET Aspire orchestration_" checkbox to add it to your Aspire project.

A thing to note here, is that gRPC runs on HTTP2, so, if you open the __appsettings.json__ file in the new project, you will see a __Kestrel__ section that looks like this 

```json
"Kestrel": {
    "EndpointDefaults": {
        "Protocols": "Http2"
    }
}
```

This tells Kestrel to use HTTP2 by default. Its also means that you cannot have regular HTTP endpoints in this project, as a browser will default to HTTTP1.1.

gPRC uses __proto__ files to define their services. A bit like __wsdl__ in the old ASP.NET Web Services days. And when you created the project, you got a service as part of the setup. Unfortunately, it defines a services called __Greeter__, which is not what you need. So, rename the __Protos/greet.proto__ file to __orders.proto__.

Next, you need to make some changes in the proto-file itself.

First of all, you want to change the `csharp_namespace` to __WebDevWorkshop.Services.Orders.gRPC__.

__Note:__ This tells the source code generator what namespace to put the generated base class for this service. Moving it to a separate namespace simplifies some things later on.

Next, change the `package` to be __orders__. This doesn't do very much that makes a big difference, but it look nicer.

Finally, remove the `service` and `message` definitions, as they aren't the signatures you need.

You should be left with this

```proto
syntax = "proto3";

option csharp_namespace = "WebDevWorkshop.Services.Orders.gRPC";

package orders;
```

Now you can start defining the methods that you want to expose. In this case, that is a single method called __OrdersService__. It should have a single method called __AddOrder__, which takes an __AddOrderRequest__ and returns an __AddOrderResponse__.

The service is defined using the `service` keyword, and the method using the `rpc` keyword

```proto
...
package orders;

service OrdersService {
    rpc AddOrder (AddOrderRequest) returns (AddOrderResponse);
}
```

Any types used as input or output, like the __AddOrderRequest__ and __AddOrderResponse__, needs to be defined as "messages" using the `message` keyword.

The __AddOrderRequest__ should consist of 3 properties. Two __Address__ properties, one called __deliveryAddress__ and one called __billingAddress__, and an array of __OrderItem__ instances.

__Note:__ The naming convention for proto-files is PascalCasing for types, and camelCasing for properties.

To create an array, you use the `repeated` keyword.

Also, during declaration, you need to the the index used for each property during serialization. It looks like this

```proto
message AddOrderRequest {
    Address deliveryAddress = 1;
    Address billingAddress = 2;
    repeated OrderItem items = 3;
}
```

Now that you have that message in place, you need create the __Address__ message. 

```proto
message Address
{
    string name = 1;
    string street1 = 2;
    string street2 = 3;
    string postalCode = 4;
    string city = 5;
    string country = 6;
}
```

And of course the __OrderItem__ message

```proto
message OrderItem
{
    string name = 1;
    int32 quantity = 2;
    float price = 3;
}
```

And finally the __OrderResponse__ message

```proto
message AddOrderResponse {
    bool success = 1;
    optional string orderId = 2;
    optional string error = 3;
}
```

__Note:__ The __OrderResponse__ message marks the __orderId__ and __error__ properties as `optional`. This is because there won't be an error if everything works as it should. But it also won't have an order id if it wasn't possible to place the order.

Now, because you renamed the service from __Greeter__ to __Orders__, the service implementation that was created during project creation, is no longer valid. ANd if you try to compile the project, it will actually fail, complaining about not finding the __Greeter__ namespace etc. So, go ahead and delete the __Services/GreetingService.cs__ file. 

You also need to open the __Program.cs__ file and remove the libe that maps the `GreeterService` as a Grpc Service.

Then create a new class in the __Services__ directory called __OrdersService__.

The next step is a bit of magic, but if you open the __WebDevWorkshop.Services.Orders.csproj__ file and locate the row that includes the __Protos/orders.proto__ file, you can see that it is defined as a Protobuf-element. This causes a source generator to automatically generate base classes for this service. 

__Note:__ Because the `GrpcServices` property is set to `Server`, it only generates the server-side part of it. Not a client.

__Note:__ This works because of the inclusion of the __Grpc.AspNetCore__ NuGet package

Go back to the __OrdersService__ and make it inherit the automagically created `OrdersService.OrdersServiceBase` base class. Now, because you changed the `csharp_namespace`, you need to reference it using `gRPC.OrdersService.OrdersServiceBase`.

```csharp
public class OrdersService : gRPC.OrdersService.OrdersServiceBase
{
    
}
```

__Tip:__ Sometimes the IDE hangs on the creation of ther service and won't give you an `OrdersService` class. So, if you cant find the `WebDevWorkshop.Orders.Services.gRPC` namespace, try restarting the IDE.

That base class has `virtual` methods for each one of the methods you defined in the proto-file. In this case, that is a single method. So you can simply override that method with the implementation you want

```csharp
public class OrdersService : gRPC.OrdersService.OrdersServiceBase
{
    public override Task<AddOrderResponse> AddOrder(AddOrderRequest request, ServerCallContext context)
    {
        return base.AddOrder(request, context);
    }
}
```

Now, before you forget about it, go and map the new service in the __Program.cs__ file. And while you are there, you might as well remove the HTTP GET endpoint as well

```csharp
var app = builder.Build();

app.MapGrpcService<OrdersService>();

app.MapDefaultEndpoints();
```

That's all you need in there...

### Adding persistence using EF Core

Once again you will need a store to put the orders in. And once again, SQL Server and EF Core are the most obvious tools for the job. So, add a the NuGet package __Aspire.Microsoft.EntityFrameworkCore.SqlServer__ to the project.

__Note:__ You are once again using the Aspire version of the package. This gives us some nice benefits as mentioned before. Things like OTEL integration and retries.

Next you need a `DbContext`. So, create a __OrdersContext__ class in a new directory called __Data__. The class should inherit from `DbContext`. And as "usual", it needs a constructor that takes a `DbContextOptions<OrdersContext>` and passes it to the base class.

```csharp
public class OrdersContext(DbContextOptions<OrdersContext> options) 
    : DbContext(options)
{
    
}
```

You will also need some "entities" to represent orders and addresses. To house those, create a new class called __Order__ in a new directory called __Entities__.

Because these classes aren't very important as such, you can just copy the contents of [this](../resources/entities.md) file into the __Order.cs__ file.

__Optional:__ If you want a bit more structure to the project layout, you can move the classes into individual file by placing your cursor on the class definition and then press __Ctrl + .__ and select __Move type to {Class Name}.cs__.

Now that the entities are in place, you need a database to store them in. For this, you need to create a migration.

Create a new class called __InitialMigration__ in the __Data__ directory. And because writing the migration is tedious, and gives you very little benefit, just replace the class of the file with this

```csharp
[Migration("001_InitialMigration")]
[DbContext(typeof(OrdersContext))]
public class InitialMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new {
                    Id = table.Column<int>()
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<string>(maxLength: 16),
                    OrderDate = table.Column<DateTimeOffset>(),
                    Total = table.Column<decimal>()
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.UniqueConstraint("UQ_Orders_OrderId", x => x.OrderId);
                }
            );

        migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new {
                    OrderId = table.Column<int>(),
                    ItemId = table.Column<int>(),
                    Name = table.Column<string>(maxLength: 128),
                    Quantity = table.Column<int>(),
                    Price = table.Column<decimal>()
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => new { x.OrderId, x.ItemId });
                    table.ForeignKey("FK_OrderItems_Orders_OrderId", x => x.OrderId, "Orders", "Id", onDelete: ReferentialAction.Cascade);
                }
            );

        migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new {
                    Id = table.Column<int>()
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(),
                    AddressType = table.Column<string>(maxLength: 16),
                    Name = table.Column<string>(maxLength: 128),
                    Street1 = table.Column<string>(),
                    Street2 = table.Column<string>(nullable: true),
                    PostalCode = table.Column<string>(),
                    City = table.Column<string>(),
                    Country = table.Column<string>()
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                    table.ForeignKey("FK_Addresses_Orders_OrderId", x => x.OrderId, "Orders", "Id", onDelete: ReferentialAction.Cascade);
                }
            );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Addresses");
        migrationBuilder.DropTable("OrderItems");
        migrationBuilder.DropTable("Orders");
    }
}
```

That takes care of the entities and the database, but you are still missing the entity mapping in the `OrdersContext`. So, open the __OrdersContext.cs__ file, and add an override to the `OnModelCreating` method in the `OrdersContext` class

```csharp
public class OrdersContext(DbContextOptions<OrdersContext> options) 
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        
    }
}
```

The mapping for the `Order` class looks like this

```csharp
modelBuilder.Entity<Order>(x =>
{
    x.ToTable("Orders");

    x.Property<int>("id");

    x.HasMany<OrderItem>("_items").WithOne().HasForeignKey("OrderId").IsRequired();
    x.HasMany<Address>("_addresses").WithOne().HasForeignKey("OrderId").IsRequired();

    x.Ignore(x => x.Items);
    x.Ignore(x => x.DeliveryAddress);
    x.Ignore(x => x.BillingAddress);

    x.HasKey("id");
});
```

As you can see, it maps it to the __Orders__ table, and uses conventions for most of it. However, the order items and addresses are mapped to internal fields. They are then exposed using some properties. And because these properties can't be set by EF, they are explicitly ignored.

__Note:__ Remember to include the namespace __WebDevWorkshop.Services.Orders.Entities__ and not __WebDevWorkshop.Services.Orders.gRPC__ to get the right `Address` and `OrderItems`.

The `OrderItem` mapping is simpler as it can handle most of the mapping using conventions

```csharp
modelBuilder.Entity<OrderItem>(x =>
{
    x.ToTable("OrderItems");

    x.Property<int>("id").HasColumnName("ItemId");
    x.Property<int>("OrderId");

    x.HasKey("id");
});
```

And finally, the `Address` mapping is once again a bit more complicated due to the inheritance from `Address` to `InvoiceAddress` and `DevliveryAddres`, which is handled using a string discriminator called __AddressType__

```csharp
modelBuilder.Entity<Address>(x =>
{
    x.ToTable("Addresses");

    x.Property<int>("Id");
    x.Property<int>("OrderId");
    x.Property<string>("AddressType");

    x.HasDiscriminator<string>("AddressType")
            .HasValue<BillingAddress>("Billing")
            .HasValue<DeliveryAddress>("Delivery");

    x.HasKey("Id");
});
```

That's all the configuration needed for the `OrdersContext`.

Before you forget, it might be a good idea to open the __Program.cs__ file and add the context to DI using the `AddSqlServerDbContext<T>()` extension method. But before you can do that, you need to set up a new database for this service.

__Note:__ Even if it would be fine to share database in this simple sample, you can't run migrations for multiple `DbContext` types in the same database. At least not without a bit of tweaking that is beyond what needs to be covered in this workshop.

Open up the __AppHost.cs__ file in the __WebDevWorkshop.AppHost__ project and locate the part where you create the __WebDevWorkshop__ database.

Currently you. are using the fluent syntax of the Aspire SQL Server resource builder to create the database. This causes some problems when you want to create 2 databases. So, you first of all need to break out the creation of the SQL Server resource builder to its own variable, and then use that to create the 2 databases. The new resource should be called __WebDevWorkshopOrders__, and the database __WebDevWorkshop.Orders__

```csharp
var sql = builder.AddSqlServer("sqlserver")
    .WithDataVolume("webdevworkshopdata")
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("WebDevWorkshop");
var ordersDb = sql.AddDatabase("WebDevWorkshopOrders", "WebDevWorkshop.Orders");
```

Now that you have 2 databases, you can add a reference to the new one from the __webdevworkshop-services-orders__ resource.

```csharp
builder.AddProject<Projects.WebDevWorkshop_Services_Orders>("webdevworkshop-services-orders")
    .WithReference(ordersDb)
    .WaitFor(ordersDb);
```

That gives you a connectionstring you can use. So, go back to the __Program.cs__ file in the __WebDevWorkshop.Services.Orders__ project and register the `OrdersContext` in DI using the `AddSqlServerDbContext<T>()` method

```csharp
builder.AddSqlServerDbContext<OrdersContext>("WebDevWorkshopOrders");
```

The last step if to make sure the migrations are being run. And as mentioned before, in development, it is fine to run them during startup.

So, open the __Program.cs__ file, and add the following code right after the creation of the web application, to run the migrations at startup

```csharp
...
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    using (var ctx = scope.ServiceProvider.GetRequiredService<OrdersContext>())
    {
        ctx.Database.Migrate();
    }
}
...
```

Now that you have a database, it is time to implement the gRPC service, which is what this lab was all about. But sometimes software development is more [yak shaving](https://sketchplanations.com/yak-shaving) than coding...

Open the __OrdersService.cs__ file and add a construtor that accepts an instance of the newly created `OrdersContext`

```csharp
public class OrdersService(OrdersContext dbContext)
 : gRPC.OrdersService.OrdersServiceBase
...
```

Then go to the `AddOrder()` method and remove the current implementation that simply calls the base class...

The first step of the real implementation, is to create a couple of `Address` instances based on the incoming information. This is a little tedious, but needs to be done

```csharp
var deliveryAddress = DeliveryAddress.Create(request.DeliveryAddress.Name, 
                                            request.DeliveryAddress.Street1, 
                                            request.DeliveryAddress.Street2, 
                                            request.DeliveryAddress.PostalCode, 
                                            request.DeliveryAddress.City, 
                                            request.DeliveryAddress.Country);

var billingAddress = BillingAddress.Create(request.BillingAddress.Name,
                                            request.BillingAddress.Street1,
                                            request.BillingAddress.Street2,
                                            request.BillingAddress.PostalCode,
                                            request.BillingAddress.City,
                                            request.BillingAddress.Country);
```

Once you have those, you can go ahead and create a new `Order` using the static `Order.Create()` method, passing in the addresses.

```csharp
var order = Order.Create(deliveryAddress, billingAddress);
```

After that, you need to create one `OrderItem` for each of items provided, and add it to the order

```csharp
foreach (var item in request.Items)
{
    order.AddItem(item.Name, item.Quantity, (decimal)item.Price);
}
```

Then you can add the order to the `OrdersContext` and save it

```csharp
dbContext.Add(order);
await dbContext.SaveChangesAsync();
```

And as you can't use `await` without marking the method `async`, you need to make the `AddOrder()` method `async`

```csharp
public override async Task<AddOrderResponse> AddOrder(AddOrderRequest request, ServerCallContext context)
...
```

That call to save it could quite easily fail, so you probably want to handle that...

Add a `try/catch` statement around the call to `SaveChangesAsync()`

```csharp
...
dbContext.Add(order);
        
try
{
    await dbContext.SaveChangesAsync();
}
catch (Exception ex)
{
}
```

As you have already made it possible to return an error in the response, you just need to create and return a `AddOrderResponse` instance, with the `Success` property set to `false`, and the `Error` property set to the error

```csharp
...
catch (Exception ex)
{
    return new AddOrderResponse
    {
        Success = false,
        Error = ex.GetBaseException().Message
    };
}
```

__Note:__ It might not be a great idea to return the raw error message like this, as it might reveal information about your environment that you don't want to reveal. 

It might also be a good idea to log a failure like this. So add an `ILogger<OrdersService>` parameter to the constructor

```csharp
public class OrdersService(
        OrdersContext dbContext, 
        ILogger<OrdersService> logger
    ) 
    : gRPC.OrdersService.OrdersServiceBase
...
```

And then add a little bit of logging in the `catch` clause before returning the response

```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Error adding order {orderId}", order.OrderId);
    ...
}
```

Yes, that is a useless log entry, but you get the point... 😂

Now, if the call to save the data succeeds on the other hand, you can return a "successful" `AddOrderResponse` with the generated order id.

```csharp
try
{
    ...
}
catch (Exception ex)
{
    ...
}

return new AddOrderResponse
{
    Success = true,
    OrderId = order.OrderId
};
```

### Verify that it "works"

Press __F5__ to start debugging. 

You should now see a new resource called __webdevworkshop-services-orders__ in the Aspire Dashboard. And if everything is running as it should, it should switch over to be in the __Running__ state after a short period of time. 

Unfortunately you will have to wait until the next lab to see if it actually works...

[<< Lab 16](../lab16/lab16.md) | [Lab 18 >>](../lab18/lab18.md)