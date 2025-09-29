var builder = DistributedApplication.CreateBuilder(args);
var ui = builder.AddContainer("ui", "zerokoll/webdevworkshop-ui")
                .WithHttpEndpoint(targetPort: 80)
                .WithHttpHealthCheck("/");
builder.AddProject<Projects.WebDevWorkshop_Web>("webdevworkshop-web","aspire")
    .WithExternalHttpEndpoints()
    .WithReference(ui.GetEndpoint("http"));
builder.Build().Run();
