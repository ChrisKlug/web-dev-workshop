var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlServer("sqlserver")
    .WithDataVolume("webdevworkshopdata")
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("WebDevWorkshop");

#pragma warning disable ASPIREINTERACTION001
var sslPasswordParameter = builder.AddParameter("ssl-password", true)
    .WithCustomInput(parameter => new()
    {
        Name = parameter.Name,
        Label = "SSL Certificate Password",
        Placeholder = "The password",
        InputType = InputType.SecretText,
        Required = true
    });
#pragma warning restore ASPIREINTERACTION001

var idsrv = builder.AddContainer("identityserver", "zerokoll/webdevworkshop-identity-server")
    .WithHttpsEndpoint(targetPort: 8081)
    .WithEnvironment("ASPNETCORE_URLS", "https://*:8081")
    .WithExternalHttpEndpoints()
    .WithBindMount(
        Path.Combine(builder.Environment.ContentRootPath, "../ssl-cert.pfx"),
        "/devcert/ssl-cert.pfx",
        true)
    .WithEnvironment("Kestrel__Certificates__Default__Path", "/devcert/ssl-cert.pfx")
    .WithEnvironment("Kestrel__Certificates__Default__Password", sslPasswordParameter);

sslPasswordParameter.WithParentRelationship(idsrv);

var ui = builder.AddContainer("ui", "zerokoll/webdevworkshop-ui")
                .WithHttpEndpoint(targetPort: 80)
                .WithHttpHealthCheck("/");

var products = builder.AddProject<Projects.WebDevWorkshop_Services_Products>("products","https")
                      .WithReference(db)
                      .WaitFor(db);

builder.AddProject<Projects.WebDevWorkshop_Web>("webdevworkshop-web","aspire")
    .WithExternalHttpEndpoints()
    .WithHttpEndpoint(name: "OrleansDashboard", env:"DashboardPort")
    .WithUrlForEndpoint("OrleansDashboard", url =>
    {
        url.DisplayText = "Orleans Dashboard";
    })
    .WithReference(ui.GetEndpoint("http"))
    .WithReference(products)
    .WaitFor(products);
    
builder.Build().Run();
