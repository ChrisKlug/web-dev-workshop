# Lab 2 - Setting up YARP

The UI for the solution requires some API endpoints to be available for it to work. However, having the UI hosted separately from the API (that you will build soon) causes some cross-origin problems in the UI. So we need to fix that...

One solution would obviously be to set up cross-origin support in the API. But...for this workshop we are going to do something else.

Instead of cross-origin support, we can make it possible for the UI to call the API on on the same host as itself. And to do that, we will use YARP to reverse proxy the UI from the application hosting the API.

## Steps (for Visual Studio)

### Add a new project

Add a new project to your solution. Make it an __ASP.NET Core Empty__ project, and call it __WebDevWorkshop.Web__.

Making sure that "_Enlist in .NET Aspire orchestration_" option is ticked on the second screen to add it to Aspire.

### Expose the HTTP-endpoints

Once the project has been created, and added to the __AppHost.cs__, you really should tell Aspire that you want to expose it publicly. This is done by calling `WithExternalHttpEndpoints()`.

```csharp
builder.AddProject<Projects.WebDevWorkshop_Web>("webdevworkshop-web")
        .WithExternalHttpEndpoints();
```

__Note:__ This doesn't actually do anything when running locally. However, if you ever deploy the application to for example Kubernetes, it will not get an ingress created unless you add this. Basically making it an internal service that can't be reached from the outside.

### Add required NuGet packages

In the newly created __webdevworkshop.Web__ project, add the following NuGet packages

 - Yarp.ReverseProxy
 - Microsoft.Extensions.ServiceDiscovery.Yarp

The first one is to be able to add YARP proxying to your project. And the second one is to enable Aspie Service Discovery then using YARP.

### Add a reference from the Web resource to the UI resource

To allow the web project to reverse proxy the UI project, we need a reference betwen the 2. This is done quite easily in Aspire. You just call the `WithReference()` extension method on the project that you want the reference to be added to, passing the resource you want to reference

__Note:__ As it is a container resource, Aspire doesn't know quite how to reference it. So, you need to explicitly reference the __http__ endpoint by calling the `GetEndpoint()` method

__Note 2:__ __http__ is the default name for an endpoint added with `WithHttpEndpoint()`

```csharp
builder.AddProject<Projects.WebDevWorkshop_Web>("webdevworkshop-web")
        .WithExternalHttpEndpoints()
        .WithReference(ui.GetEndpoint("http"));
```

### Add YARP forwarding

Once the reference has been added, enabling Aspire service discovery, it is simply a matter of adding the required YARP services, and set up the reverse-proxying.

Open the __Program.cs__ file in the __WebDevWorkshop.Web__ project and add the required YARP service by calling

```csharp
builder.Services.AddHttpForwarderWithServiceDiscovery();
```

Next, replace the `app.MapGet("/", () => "Hello World!");` with the following

```csharp
app.MapForwarder("/{**catch-all}", "https+http://ui");
```

This will set up a forwarder that forwards any request that hasn't already been handled by the request pipeline to the __ui__ resource.

### Fix API forwarding issue

Unfortunately the current forwarder would forward any incorrect API call to the UI resource as well. This would make debugging it really hard. So, you need to make sure that any unhandled call to __/api/*__ returns 404. 

This can be done by handling all these requests manually before mapping the YARP forwarder.

Before the call to `MapForwarder()`, add the following code

```csharp
app.Map("/api/{**catch-all}", (HttpContext ctx) => {
    ctx.Response.StatusCode = 404;
});
```

Now you just need to make sure that any API endpoint you create is added before this handler...

### Verify that it works

Press F5 to start the project.

In the Aspire Dashboard, make sure you are seeing 2 resources (the UI and the Web).

Click on the HTTPS link for Web resource. This should open a new tab, in which you see the (broken) UI, as the call should be reverse-proxied for you.

__Note:__ Depending on you resolution, the HTTP link might be hidden as a __+1__ instead of the actual link.

Go back to the Dashboard and click on the Web resource to open the details pane. In the new pane, scroll down to find the `services__ui__http__0` environment variable. This is the thing that makes the Aspire service discovery work.

Next, go to the "Structured Logs" section and select the Web project in the drop-down. This should show you the logs being output from the project. 

Feel free to browse around and look at the traces and metrics as well.

### Remove HTTP endpoint

If you go to the resources section of the Aspire Dashboard, you can see that the Web project has an HTTP-based URL. However, we should really stick to HTTPS. Luckily, Aspire uses the `launchSettings.json` file to set this up. 

Stop the debugging session and open the __Properties/launchSettings.json__ file in the __WebDevWorkshop.Web__.

Duplicate the `https` profile, and rename it to `aspire`. Then update the `applicationUrl` to only include the https endpoint

```csharp
"aspire": {
      …
      "applicationUrl": "https://localhost:XXXX",
}
```

__Tip:__ If you don't want the browser to launch at every run, you can also set the `launchBrowser` to `false`

Now you just need to tell Aspire that you want to use this new profile.

Open the __AppHost.cs__ file in the __WebDevWorkshop.AppHost__ project. 

Update the `AddProject()` call to pass in the launch profile name as a second parameter

```csharp
builder.AddProject<Projects.WebDevWorkshop_Web>(..., "aspire")
        ...;
```

Press __F5__ to start the solution, and verify that the web resource now exposes an HTTPS-based URL.

[<< Lab 1](../lab1/lab1.md) | [Lab 3 >>](../lab3/lab3.md)