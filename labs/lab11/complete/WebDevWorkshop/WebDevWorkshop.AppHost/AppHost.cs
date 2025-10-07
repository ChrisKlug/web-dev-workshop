var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlServer("sqlserver")
    .WithDataVolume("webdevworkshopdata")
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("WebDevWorkshop");

var ui = builder.AddContainer("ui", "zerokoll/webdevworkshop-ui")
                .WithHttpEndpoint(targetPort: 80)
                .WithHttpHealthCheck("/");

var products = builder.AddProject<Projects.WebDevWorkshop_Services_Products>("products","https")
                      .WithReference(db)
                      .WaitFor(db);

builder.AddProject<Projects.WebDevWorkshop_Web>("webdevworkshop-web","aspire")
    .WithExternalHttpEndpoints()
    .WithReference(ui.GetEndpoint("http"))
    .WithReference(products)
    .WaitFor(products);
    
builder.Build().Run();
