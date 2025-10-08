extern alias SERVER;
using SERVER::WebDevWorkshop.Services.Orders.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
