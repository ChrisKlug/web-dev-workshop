# Lab 20: Adding Custom Data to the OTEL Traces

One of the best things about using Aspire is the Dashboard. And a big part of that is that the Aspire Dashboard works as an Open Telemetry endpoint, getting all OTEL data sent by the different resources in the solution.

But you can make it even better by extending the OTEL data being sent from the different parts of the application.

## Steps (for Visual Studio)

###  Adding information to Traces

OTEL traces are made up of one or more spans. Each span represents a part of the flow through the application. For example, when a request comes in to the web project, a span is started. And then, when a request to the database is made, that is another child span. And a call to another service is another child span.

Spans can contain information about the execution. By default, the information is pretty basic. But, you can add your own data to spans to make them more useful.

The easiest way, is to simply add information to the spans that are implicitly created for you. 

Let's add some information to the span that is created when calling the `AddOrder()` method in the __OrdersService__ class.

Open the __OrdersService.cs__ in the __WebDevWorkshop.Services.Orders__ project, and go to the `AddOrder()` method. At the top of the method, you should add a "tag" called __ServiceName__ to the current span. The value should be the name of the service.

__Note:__ A tag is simply a key value pair that is added to a span.

In ASP.NET Core, an OTEL span is represented by an instance of `System.Diagnostics.Activity`. And, the `Activity` class has a static property called `Current`, which gives you access to the current `Activity`.

So, to add a tag called __ServiceName__ to the current span, you simply call the `SetTag()` method on the current `Activity`

```csharp
public override async Task<AddOrderResponse> AddOrder(AddOrderRequest request, ServerCallContext context)
{
    Activity.Current?.SetTag("ServiceName", nameof(OrdersService));
    ...
}
```

__Note:__ The `Current` property is nullable, as there might not be an `Activity` available. 

You can also add "events" to a span. An event is a thing that has happened. For example, an order was placed.

__Note:__ The tags and events can be queried when looking for information about the system, and its execution.

So, let's add an event when an order is being added.

Look a little further down the `AddOrder` method to locate the line of code that creates the `Order`.

Right after the creation of the order, add an event by calling the `AddEvent()` method on the current `Activity`.

```csharp
...
var order = Order.Create(deliveryAddress, billingAddress);

Activity.Current?.AddEvent(new ActivityEvent("Adding Order"));
...
```

__Note:__ You can also add tags to an event if you want to provide more information than just the fact that something happened.

### Verifying that adding information to OTEL works

Press __F5__ to start debugging. 

In the website, Go through the steps needed to create an order. Then, go back to the Aspire Dashboard, and the __Traces__ section and locate the trace that represents the __POST__ request to the __api/Orders__ endpoint, and click on it. 

You should see a trace with several spans in it. Locate the span that represents the call to the __orders__ service, and click on it to open the details pane for that call. Under the __Span__ section, you should be able to find a key/value pair with the key __ServiceName__. And if you scroll down to the __Events__ section, there should be a single event called __Adding Order__. 

If you find these, it is working!

### Adding custom spans

You can also add your own spans to highlight specific parts of the request execution. This can make it easier to read the spans to see the "intent", as well as get extra information. 

For example, you automatically get the execution time for any span in a trace. This can help identifying parts of the execution that are slow.

Adding a custom span/activity is fairly easy. 

The first step is to create an `ActivitySource`. This can then be used to create an `Activity`.

Go to the top of the `OrdersService` class, and add a private, static `ActivitySource` instance.

```csharp
public class OrdersService(...) 
{
    public const string ActivitySourceName = "OrdersService";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    ...
}
```

Once you have the `ActivitySource`, it is time to start a new activity to represent the adding of an order.

Locate the line of code where you added the __Adding Order__ event, and create a new `Activity` by calling `ActivitySource.StartActivity()` right after it. Call it __OrderService.AddOrder__. This will create a new `Activity` as a child to the current one.

The way you control the length of the activity/span, is by using the `IDisposable` interface that the `Activity` implements. As soon as the `Activity` is disposed, the activity ends, and the parent becomes the current one.

```csharp
...
Activity.Current?.AddEvent(new ActivityEvent("Adding Order"));

using var activity = ActivitySource.StartActivity("OrderService.AddOrder");
```

It might be interesting to add a tag to the activity as well. So, go ahead and add a __ServiceName__ tag to this activity as well

```csharp
...
using var activity = ActivitySource.StartActivity("OrderService.AddOrder");
        
activity?.SetTag("ServiceName", nameof(OrdersService));
...
```

And why not add an event to it as well. But instead of just adding an event like you did before, add an event that includes tags for the order id and order date. The tags are added by passing in a `ActivityTagsCollection` to the __tags__ parameter of the `ActivityEvent` constructor

```csharp
...
activity?.SetTag("ServiceName", nameof(OrdersService));
activity?.AddEvent(new ActivityEvent("Adding Order", 
    tags: new ActivityTagsCollection([
        new KeyValuePair<string, object?>("OrderId", order.OrderId),
        new KeyValuePair<string, object?>("OrderDate", order.OrderDate)
    ])));
...
```

That's it! This will add another span/activity to the trace, and include the information you added to it. However, not all activities are picked up by OTEL by default. 

When you create custom `ActivitySources` like this, you need to tell OTEL to include it.

Open the __Program.cs__ file in the __WebDevWorkshop.Services.Orders__.

Right before the creation of the `WebApplication`, you need to tell OTEL to listen to activities from your newly created `ActivitySource`. And the way to do that, is to call `AddOpenTelemetry()` on the `IServiceCollection`. On the returned `OpenTelemetryBuilder`, call `WithTracing()` to configure the tracing for this service. In the callback to the `WithTracing()` method, you need to add your custom source.

```csharp
...
builder.Services.AddGrpc();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
        tracing.AddSource(OrdersService.ActivitySourceName)
    );

var app = builder.Build();
...
```

### Verifying that adding custom spans work

Press __F5__ to start debugging. 

In the website, Go through the steps needed to create an order.

After the order has been placed, go back to the Aspire Dashboard, and the __Traces__ section.

You should now see one more span that represents your custom activity. And if you open the details pane, you should see both the __ServiceName__ tag, and the __Adding Order__ event, including the order information tags you added to the event.

### Adding OTEL metrics

OTEL doesn't just support traces. It supports metrics as well. And custom metrics can actually be a really good way to see if your environment is having issues. Or, just observe the usage.

So, let's try and add a metric to keep track of orders being added.

Create a new directory called __Observability__ in the __WebDevWorkshop.Services.Orders__ project, and then a new class called __OrdersMetrics__ inside that directory. This class will be the way to interact with the metric. 

An OTEL metric can take a few different forms, for example a counter, a histogram or a gauge. In this case you will use a counter, to keep track of the number of orders being added.

The representation of a metric in C#, is an instance of `Meter`. From the `Meter` you can then create a counter or gauge etc.

Each `Meter` needs a unique name. So, go ahead and add a public, constant string called __MeterName__ with the value __WebDevWorkshop.Services.Orders__

```csharp
public class OrdersMetrics
{
    public const string MeterName = "WebDevWorkshop.Services.Orders";
}
```

To create the `Meter`, you need to get hold of the `IMeterFactory`. And to do that, you need a constructor that takes it as a parameter. Inside the constructor you can then create a new `Meter` by calling the `IMeterFactory` instance's `Create()` method, passing in the name of the meter

```csharp
public class OrdersMetrics
{
    public const string MeterName = "WebDevWorkshop.Services.Orders";

    public OrdersMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
    }
}
```

You don't need to keep track of the `Meter` instance in this case. But you do need it to create the `Counter<int>` instance that you will use to keep track of the added orders. 

When you create counter, you only need to provide a name. You can optionally provide a unit and description as well. 

In this case, it makes sense to set the name to __orders__, the unit to __Order__ and the description to __Orders added__. And as the counter is a thing you need to use over time, add a private property called __TotalOrdersCounter__ to hold the instance.

```csharp
public class OrdersMetrics
{
    public const string MeterName = "WebDevWorkshop.Services.Orders";

    public OrdersMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        TotalOrdersCounter = meter.CreateCounter<int>("orders", "Order", "Orders added");
    }

    private Counter<int> TotalOrdersCounter { get; }
}
```

The last step is to add a method called __AddOrder()__ so that you can notify the counter that a new order has been added. All you need to do inside the __AddOrder()__ method is to call the `Add()` method on the `TotalOrdersCounter`.


```csharp
public class OrdersMetrics
{
    public const string MeterName = "WebDevWorkshop.Services.Orders";

    public OrdersMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
    }

    public void AddOrder() => TotalOrdersCounter.Add(1);

    private Counter<int> TotalOrdersCounter { get; }
}
```

Ok, that's _almost_ everything you need to do to add a custom metric to your OTEL data. However, there are two more things you need to do...

First of all, you need to register your newly created `OrdersMetrics` class as a singleton instance in the service collection.

Open the __Program.cs__ file and add a singleton `OrdersMetrics` instance to the service collection right before the OTEL configuration

```csharp
...
builder.Services.AddSingleton<OrdersMetrics>();

builder.Services.AddOpenTelemetry()
    .WithTracing();
...
```

Secondly, you need to tell OTEL that you want to track this metric. Just like you had to tell it that you wanted to use the `ActivitySource` earlier in this lab.

After the call to `WithTracing()`, add call to `WithMetrics()`. Pass in a callback that adds the custom metrics name to the metrics options

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(...)
    .WithMetrics(options=> 	{
        options.AddMeter(OrdersMetrics.MeterName);
    });
```

Ok, now the new metric is configured for use with the OTEL service. The last step is to tell it when orders are added to the system.

Open the __OrdersService.cs__ file, and add another constructor parameter called __metrics__, of the type `OrdersMetrics`

```csharp
public class OrdersService(
        OrdersContext dbContext, 
        OrdersMetrics metrics,
        ILogger<OrdersService> logger
    ) 
```

Then locate the code where the order is saved to the database, and add a call to `metrics.AddOrder()` as soon as the order has been saved.

```csharp
...
await dbContext.SaveChangesAsync();
metrics.AddOrder();
...
```

That's it!

### Verifying that it works

Press __F5__ to start debugging, and then add a couple of orders to the system.

Then go to the Aspire Dashboard, and the __Metrics__ section. In the __Resource__ drop-down, choose the __orders__ resource. Then locate the __orders__ metric under the __WebDevWorkshop.Services.Orders__ section.

Clicking on that metric should give you a graph displaying the total number of orders added. And if you switch over to the __Table__ tab, you get the same data, but split into time slots.

If you want to, you can go back to the website and add another order. It should show up in the metrics data almost immediately.

[<< Lab 19](../lab19/lab19.md) | [Lab 21 >>](../lab21/lab21.md)