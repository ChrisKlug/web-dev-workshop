using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebDevWorkshop.Services.Products.Data;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework(
    "WebDevWorkshop.Services.Products.Tests.Infrastructure.TestRunStart",
    "WebDevWorkshop.Services.Products.Tests"
)]

namespace WebDevWorkshop.Services.Products.Tests.Infrastructure;

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
