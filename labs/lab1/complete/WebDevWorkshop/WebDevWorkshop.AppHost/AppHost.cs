var builder = DistributedApplication.CreateBuilder(args);

var ui = builder.AddContainer("ui", "zerokoll/webdevworkshop-ui")
                .WithHttpEndpoint(targetPort: 80)
                .WithHttpHealthCheck("/");

builder.Build().Run();
