# [Optional] Lab 21: Outbox Pattern using EF Core Interceptors

In this, optional, lab, you will have a look at implementing the "outbox pattern" using Entity Framework Core interceptors.

The pattern is useful for a lot of things, but you might actually take something else away from this lab as well. And that is the fact that interceptors in EF are really powerful and useful for a lot of scenarios. Not to mention, fairly easy to implement.

### What is the outbox pattern?

The outbox pattern is a design pattern that allows you to safely send messages that are dependent on database changes for example. 

Any time you need to use a service that does not support transactions, together with one that does, you end up in a slightly scary place. You need to make sure that both parts go through successfully, or fail together. 

As an example, sending an e-mail when an order has been placed. E-mail servers don't support transactions in most cases. So, do you persist the order in the database first, or do you send the e-mail first? What happens if you persist the data, and then the e-mail fails? Or, what happens if you send the e-mail and the persistence fails? Or, what happens if... There are a lot of variations on this...

The "simple" solution is to store the e-mail information in the database at the same time as the order is persisted. And then have a second service that is responsible for sending the e-mail. This means that the data is always persisted together, in a single transaction, which is safe. The second service can then send the e-mail.

Worst case scenario, more than one order confirmation e-mail is sent because there are issues with the e-mail sending. But that is not a huge deal in a lot of situations.

### EF Interceptors

EF interceptors are small pieces of code that can be used to intercept database actions like creating the database connection, saving data or starting a transaction. These are registered with the `DbContext` and will always run. This means that creating "events" based on database changes can be done in an easy way. 

For example, you can change the last modified date and who did it automatically on every save. Or add an event to send an e-mail whenever an order is placed. Taking a load off the client by automating some of the functionality.

## Steps (for Visual Studio)

###  Adding an "event" when an order is placed

The goal of this lab is to add an "event" to a table in the database whenever an order is added. This event could then be picked up and used by a service to send an e-mail to the customer. Or add a work order to pack the things. Or...yeah...whatever needs to happen when an order is added. And it wouldn't be up to the client to remember to do it.

The first step is to add a new table in the database called __Events__. And for that, you will need a new migration. 

Add a new class called __EventsMigration__ in the __WebDevWorkshop.Services.Orders__ project's __Data__ directory. Then replace the class with the following code to create a migration that creates the __Events__ table.

```csharp
[Migration("002_EventsMigration")]
[DbContext(typeof(OrdersContext))]
public class EventsMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new {
                    Id = table.Column<int>()
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(maxLength: 32),
                    Date = table.Column<DateTimeOffset>(),
                    Data = table.Column<string>(),
                    State = table.Column<string>(),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                }
            );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Events");
    }
}
```

You will also need a class to represent an event. And, if you make it mimic the table you just created, the mapping will be simple.

Add another class called __Event__ in the __Entities__ directory.

The implementation should look like this to replicate the structure of the table

```csharp
internal class Event
{
    public int Id { get; set; }
    public EventType Type { get; set; }
    public DateTimeOffset Date { get; set; }
    public string Data { get; set; } = string.Empty;
    public EventState State { get; set; }
}

public enum EventType
{
    OrderCreated
}

public enum EventState
{
    Pending,
    Handled
}
```

__Note:__ It is a bit more complex than needed, using an `EventType` enum for example. But it shows you how it could be extended to support different type of events.

__Note:__ The `EventState` for an event is set to `Pending` on creation, and then moved to `Handled` whenever the other service has handled it. 

Now that you have a database table and an entity to enable you to persist an "event", it is time to create the event whenever an order is added.

Create a new class called __OrderCreatedInterceptor__ in the __Data__ directory.

The new class should inherit from `SaveChangesInterceptor`, as you are interested in intercepting calls to save changes.

__Note:__ You can either inherit from ready made base classes like `SaveChangesInterceptor` or `DbCommandInterceptor`. Or, you can manually implement the corresponding interfaces `ISaveChangesInterceptor` and `IDbCommandInterceptor`.

__Note:__ Since the interfaces have default implementations, the difference between implementing the interface and inheriting a base class has been muddled a lot.

In this case, you want to override the `SavingChangesAsync()` method, as you want to act on the information before it is written to the database. That way, your changes will be committed in the same transaction.

__Note:__ There is also a `SavedChangesAsync()` method that is invoked after the data has been persisted. As well as a bunch of other methods...

```csharp
public class OrderCreatedInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = new CancellationToken())
    {
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
```

The implementation is based on finding out if any `Order` entities are being added to the database. To see this, you can use the context's change tracker. This keeps track of any tracked entity in the context, allowing you to query it for changes. In this case, you are looking for any entity of the type `Order` that is in the `EntityState.Added` state.

```csharp
public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = new CancellationToken())
{
    var events = eventData.Context!.ChangeTracker.Entries<Order>()
        .Where(x => x.State == EntityState.Added)
        .ToArray();

    return base.SavingChangesAsync(eventData, result, cancellationToken);
}
```

Now, you aren't really interested in that entity. You are interested in creating an `Event` for each one of the entities you find in the change tracker. 

So, add a `Select()` to the end of the query to create an `Event` for each added `Order`

```csharp
public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = new CancellationToken())
{
    var events = eventData.Context!.ChangeTracker.Entries<Order>()
        .Where(x => x.State == EntityState.Added)
        .Select(x => new Event
        {
            Type = EventType.OrderCreated,
            State = EventState.Pending,
            Date = DateTimeOffset.UtcNow,
            Data = x.Entity.OrderId
        })
        .ToArray();

    return base.SavingChangesAsync(eventData, result, cancellationToken);
}
```

Once you have the `Event` entities you need, you just need to attach it to the `DbContext`.

As the context is already in the process of saving stuff, the newly added `Event` entities will automatically be added to the database in the same transaction. Just as you need.

```csharp
        ...
        .ToArray();

    eventData.Context!.AddRange(events);

    return base.SavingChangesAsync(eventData, result, cancellationToken);
}
```

You are still missing 2 things. You haven't configured the `Entity` in the `DbContext`, so it wouldn't know what to do with those entities that you added. And you haven't registered the interceptor in the `DbContext`.

Let's start with the entity configuration.

Open the __OrdersContext.cs__ file, and locate the `OnModelCreating()` method. At the bottom of the method, after the existing entity mappings, add the mapping for your newly created `Event` entity

```csharp
    ...
    modelBuilder.Entity<Address>(...);
    
    modelBuilder.Entity<Event>(x =>
    {
        x.ToTable("Events");

        x.Property(x => x.Type).HasColumnName("EventType").HasConversion(x => x.ToString(), x => Enum.Parse<EventType>(x));
        x.Property(x => x.State).HasConversion(x => x.ToString(), x => Enum.Parse<EventState>(x));

        x.HasKey("Id");
    });
}
```

The final step is to register the interceptor in the `DbContext`. And since the interceptor in this case has no state, it is best to register it as a singleton. And for that, you need a singleton instance of the interceptor. Luckily, that is a piece of cake to set up.

Open the __OrderCreatedInterceptor.cs__ file, and add a static `OrderCreatedInterceptor` property called __Instance__ at the bottom of the `OrderCreatedInterceptor` class. And set it to a new instance by default

```csharp
public class OrderCreatedInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(...)
    {
        ...
    }
    
    public static OrderCreatedInterceptor Instance { get; } = new();
}
```

Once you have the singleton instance that you need, you need to add it to the `DbContext` configuration. There are a couple of ways of doing this. One is to override the `OnConfiguring()` method on the `DbContext`. The other is to add it during configuration. In this case, you might as well just do it during configuration.

In the __Program.cs__ file, in the __WebDevWorkshop.Services.Orders__ project, find the code that registers the `OrdersContext` in the service collection.

The `AddSqlServerDbContext()` method has an optional parameter called __configureDbContextOptions__. It is of the type `Action<DbContextOptionsBuilder>?`, and can be used to configure the context.

Add a new callback to this parameter

```csharp
builder.AddSqlServerDbContext<OrdersContext>("WebDevWorkshopOrders",
    configureDbContextOptions: options =>
    {
        
    });
```

In the callback, add your singleton `OrderCreatedInterceptor` to the options by calling the `AddInterceptors()` method

```cshap
builder.AddSqlServerDbContext<OrdersContext>("WebDevWorkshopOrders",
    configureDbContextOptions: options =>
    {
        options.AddInterceptors(OrderCreatedInterceptor.Instance);
    });
```

That's it... Now, how do you verify this?

### Verifying that it works

There are 2 ways of verifying that the interceptor works. One is to start the application, add an order and then look in the database to see if an event has been created. And that works...but it is very manual. A better way is to write a test that verifies that it works.

Open the __OrderServiceTests.cs__ file, in the __WebDevWorkshop.Services.Orders.Tests__ project, and add a new test at the bottom of the `OrdersServiceTests` class. The test should be called __Generates_an_event__ and call the `TestHelper.ExecuteTest()` method just like the other tests

```csharp
[Fact]
public Task Generates_an_event()
    => TestHelper.ExecuteTest<SERVER::Program, OrdersService.Data.OrdersContext, gRPC.OrdersService.OrdersServiceClient>(
        test: async client =>
        {

        }, 
        validateDb: async cmd =>
        {
            
        });
```

The code to test should create an `AddOrderRequest` and pass it to the `AddOrderAsync()` method. However, in this case, you don't really need to add any items to the order, as that doesn't matter

```csharp
test: async client =>
{
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

    await client.AddOrderAsync(request);
}, 
```

In this case, the database validation is much more interesting. However, it is a lot of fairly basic code. So, you just need to copy it from here

```csharp
validateDb: async cmd =>
{
    int id;
    string orderId;
    cmd.CommandText = "SELECT * FROM Orders";
    using (var reader = await cmd.ExecuteReaderAsync())
    {
        reader.Read();
        id = (int)reader["Id"];
        orderId = (string)reader["OrderId"];
    }
    cmd.CommandText = "SELECT * FROM Events";
    using (var reader = await cmd.ExecuteReaderAsync())
    {
        Assert.True(reader.Read());
        Assert.Equal("OrderCreated", (string)reader["EventType"]);
        Assert.Equal("Pending", (string)reader["State"]);
        Assert.Equal(orderId, (string)reader["Data"]);
        Assert.False(reader.Read());
    }
}
```

As you can see, it gets the added order from the database, and then looks for a corresponding __OrderCreated__ event in the __Events__ table.

Use the Test Explorer to run the test to verify that it works as expected.

It does! But it shouldn't... 

You are currently not adding the interceptor to the `DbContext` in the `TestHelper`, so, it really shouldn't be working. But it is...

The reason is that when you `AddSqlServerDbContext<OrdersContext>()` during startup of the application, it adds the configuration callback as a `IDbContextOptionsConfiguration<OrdersContext>` in the service container. 

This causes the callback to be called, a bit unexpectedly, even if you have removed both the `TDbContext` and `DbContextOptions<TDbContext>` registrations during test set up in the `TestHelper`.

To fix this, you need to remove that `IDbContextOptionsConfiguration<TDbContext>` registration as well.

Open the __TestHelper.cs__ file in the __WebDevWorkshop.Testing__ project, and located the `ExecuteTest()` method that you are using in the `Generates_an_event()` test.

__Note:__ It is the one that takes 4 parameters.

__Tip:__ The easiest way to locate the correct method, is actually to go to the `Generates_an_event()` test, put the caret on the `TestHelper.ExecuteTest<...>()` method name and press __F12__ (Go to implementation).

Once you have located the correct `TestHelper.ExecuteTest()` method, find the code that removes the `TDbContext` and `DbContextOptions<TDbContext>` registrations.

Right after retrieving the descriptors for these types, add code to retrieve the `IDbContextOptionsConfiguration<TDbContext>` as well.

```csharp
var dbDescriptor = services.First(x => x.ServiceType == typeof(TDbContext));
var optionsDescriptor = services.First(x => x.ServiceType == typeof(DbContextOptions<TDbContext>));
var optionsConfigDescriptor = services.First(x => x.ServiceType == typeof(IDbContextOptionsConfiguration<TDbContext>));
```

And then, after the code that removes the 2 previous descriptors, add a line to remove this one as well

```csharp
services.Remove(dbDescriptor);
services.Remove(optionsDescriptor);
services.Remove(optionsConfigDescriptor);
```

Try running the test again. It should fail now.

Now that it is failing, you know that it isn't running the config in the `Program` class. Instead, you can now add the interceptor to the `DbContext` registration in the `TestHelper` as you want.

However, there is a slight problem. You cannot reference the __WebDevWorkshop.Services.Orders__ project from the __WebDevWorkshop.Testing__ project, as it would end up with 2 `Program` classes in the __WebDevWorkshop.Services.Products.Tests__ project. One from the __WebDevWorkshop.Services.Products__ service, and one from the downstreams imported imported __WebDevWorkshop.Services.Orders__.

The easiest solution to this, is to add another callback that is used to configure the `OrdersContext`.

So, open the __TesteHelper.cs__ file, and locate the `ExecuteTest()` method you just added. In that method, declare another callback called __configureDbContext__, taking a `DbContextOptionsBuilder` instance. It doesn't need to return task or anything, as it will only be used to configure the context

```csharp
public static async Task ExecuteTest<TProgram, TDbContext, TGrpcService>(
    Action<DbContextOptionsBuilder> configureDbContext,
    Func<TGrpcService, Task> test,
    Func<DbCommand, Task>? dbConfig = null,
    Func<DbCommand, Task>? validateDb = null
)
```

And then, in the `ConfigureTestServices()` callback, right after calling `options.UseSqlServer()`, add a call to the `configureDbContext` callback

```csharp
...
services.AddDbContext<TDbContext>((services, options) =>
{
    ...
    options.UseSqlServer(config.GetConnectionString("WebDevWorkshopOrders"), ...);
    configureDbContext(options);

}, ...);
...
```

The last step is to update the __Adds_order_to_db__  and __Generates_an_event__ tests.

Open the __OrdersServiceTests.cs__ file in the __WebDevWorkshop.Services.Orders.Tests__ project and update the calls to the `TestHelper.ExecuteTest()` method to include the `DbContext` configuration.

You don't need the interceptor in the __Adds_order_to_db__ test, so it only needs the following

```csharp
public Task Adds_order_to_db()
=> TestHelper
    .ExecuteTest<SERVER::Program, OrdersService.Data.OrdersContext, gRPC.OrdersService.OrdersServiceClient>(
        configureDbContext: x => {},
        test: async client => { ... }
    );
```

The __Generates_an_event__ on the other hand, do need the `OrderCreatedInterceptor`, so the callback ends up like this

```csharp
public Task Generates_an_event()
    => TestHelper.ExecuteTest<SERVER::Program, OrdersService.Data.OrdersContext, gRPC.OrdersService.OrdersServiceClient>(
        configureDbContext: x => x.AddInterceptors(OrderCreatedInterceptor.Instance),
        test: async client => { ... }
    );
```

Now, if you run the test again, it should succeed! And this time, it should succeed as expected, not because weird config stuff that is a bit unexpected.

[<< Lab 20](../lab20/lab20.md) | [Lab 22 >>](../lab22/lab22.md)