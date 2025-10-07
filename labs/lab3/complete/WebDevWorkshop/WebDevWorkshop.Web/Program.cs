var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpForwarderWithServiceDiscovery();

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Map("/api/{**catch-all}", (HttpContext ctx) => {
    ctx.Response.StatusCode = 404;
});

app.MapForwarder("/{**catch-all}", "https+http://ui");

app.Run();
