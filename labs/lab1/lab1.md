# Lab 1 - Setting up Aspire

In this lab, you will set up a new .NET solution with Aspire as the host for the services to come.

## Steps (for Visual Studio/Rider)

### Install / Update Aspire Templates

If you do not have the Aspire templates installed on your machine, or they are not the latest version, you need to install/update those first. This is done (somewhat) easily using the following CLI command:

```bash
dotnet new install Aspire.ProjectTemplates
```

__Note:__ You can verify successful installation by running `dotnet new list aspire`. This should list a few templates for Aspire projects.

### Create a new solution/project

Open Visual Studio and select __Create a new project__. Then search for and select the __.NET Aspire Empty App__ template.

Name the project __WebDevWorkshop__.

_Comment:_ If you have never worked with Aspire before, feel free to have a look through the included source code to see if you can find your way around it.

### Add a Docker image resource

Open the __AppHost.cs__ file in the __WebDevWorkshop.AppHost__ project. 

After the creation of the `DistributedApplicationBuilder`, add a new container resource called __ui__ using the Docker image `zerokoll/webdevworkshop-ui`. Assign the returned `ResourceBuilder<T>` to a variable called `ui`.

However, as it is a container resource, Aspire has no knowledge of what ports to expose. So, you need to map a port on your local machine to port 80 in the container. This is done using the `WithHttpEndpoint` extension method. And since we only care about the target port (the port inside the container), you only need to pass in the `targetPort` parameter.

```csharp
var ui = builder.AddContainer("ui", "zerokoll/webdevworkshop-ui")
                .WithHttpEndpoint(targetPort: 80);
```

### Verify that it works

Now that the container resource has been added, you should be able to start your application by pressing __F5__.

__Note:__ It can take a long time sometimes to get it started. Make sure that your Docker Desktop hasn't gone into _Resource Saver Mode_. If it has, try turning that off. More information can be found [here](https://docs.docker.com/desktop/use-desktop/resource-saver/).

This should open a browser showing you the Aspire Dashboard. And on the Dashboard, you should see a single resource called __ui__.

The _State_ might read as "Runtime unhealthy" until the Docker image has been downloaded. To check on the progress of the download, click the __Console Logs__ button ![](./resources/console-logs-button.png) on the right-hand side of the dashboard.

Once the __ui__ resource is in the "Healthy" state, click on the __localhost:XXXXX__ URL to view the UI.

__Note:__ The UI doesn't actually work at the moment, as it requires some API endpoints to exist... So you will see a __Whoopsi Daisy!__ error message. But that is okay for now.

### Optional: Fix the Dashboard state

There is a small problem that might arise if you are very quick. 

When you start up Aspire, if you look at the Dashboard, you will notice that the __ui__ resource gets the state __Running__ almost immediately. However, when the tab that shows the website opens, it takes a bit of time before the page loads. 

This is because Aspire only checks to see if the container is up and running, not that the application inside it is. To fix that, you can go ahead and add a health check to the __ui__ resource. This will ping the defined path over and over again. And as long as it doesn't return HTTP 2XX, it will be considered unhealthy.

__Note:__ This obviously also works if the resource goes offline for some reason.

Open the __AppHost.cs__ file, and add a call to the `WithHttpHealthCheck()` method

```csharp
var ui = builder.AddContainer("ui", "zerokoll/webdevworkshop-ui")
    .WithHttpEndpoint(targetPort: 80)
    .WithHttpHealthCheck("/");
```

Press __F5__ again, and notice how the __ui__ resource stays __Unhealthy__ for a little while before switching to __Running__.

__Note:__ It might be starting too fast to notice, but now you know that you have that check in place at least.

__Note:__ There is also a `WithHealthCheck()` method that allows you to create custom health checks that aren't HTTP-based.

[<< Home](../../readme.md) | [Lab 2 >>](../lab2/lab2.md)
