# Lab 1 - Setting up Aspire

In this lab, you will set up a new .NET solution with Aspire as the host for the services to come.

## What are we doing?

You need to create a new __Aspire Empty App__ called __WebDevWorkshop__. Inside this project, you then need to add add a container resource called `ui` using the image called `zerokoll/webdevworkshop-ui`. The resource should expose its port 80 on any port on your machine.

## Steps (for Visual Studio/Rider)

### Install / Update Aspire Templates

If you do not have the Aspire templates installed on your machine, or they are not the lates version, you need to install/update those first. This is done (somewhat) easily using the following CLI command

```bash
dotnet new install Aspire.ProjectTemplates
```

__Note:__ You can verify successful installation by running `dotnet new list aspire`. This should list a few templates for Aspire projects.

### Create a new solution/project

Open Visual Studio and select __Create a new project__. Then search for and select the __.NET Aspire Empty App__ template.

Name the project __WebDevWorkshop__.

_Comment:_ If you have never worked with Aspire before, feel free to have a look through the included source code to see that you can find your way around it.

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

This should open a browser showing you the Aspire Dashboard. And on the Dashboard, you should see a single resource called __ui__.

The _State_ might read as "Runtime unhealthy" until the Docker image has been downloaded. To check on the progress of the download, click the __Console Logs__ button ![](resources/console-logs-button.png) on the right-hand side of the dashboard.

Once the __ui__ resource is in the "Healthy" state, click on the __localhost:XXXXX__ URL to view the UI.

__Note:__ The UI doesn't actually work at the moment, as it requires from API endpoints to exist...

[<< Home](./readme.md) | [Lab 2 >>](./lab2.md)
