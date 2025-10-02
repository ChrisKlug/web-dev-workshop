# Lab 22: Optional: Deploying using Docker Compose

Aspire publishing is still very much an experimental feature that moves around a bit. However, in this tiny lab you will see what it means to add publishing to your Aspire-based solutions.

## Steps (for Visual Studio)

###  Adding support for Docker-Compose publishing

When you publish an Aspire-solution, what happens is very much dependent on the publisher you have chosen. In this case, you will use a Docker-Compose publisher that will generate the required Docker images, and produce a __docker-compose.yaml__ file for you to use.

Start by adding the __Aspire.Hosting.Docker__ NuGet package to the __WebDevWorkshop.AppHost__ project. 

__Note:__ At the time of writing, this NuGet package is a pre-release package. So don't forget to tick the "include pre-releases" checkbox.

Then open the __AppHost.cs__ file, and add the Docker-Compose publisher to the AppModel using the method called __AddDockerComposeEnvironment()__

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("aspire-docker-demo");
...
```

That's it! 

If you want to make changes to the generated __docker-compose.yaml__ file, you can use the `ConfigureComposeFile()` method

```csharp
builder.AddDockerComposeEnvironment("aspire-docker-demo")
    .ConfigureComposeFile(composeFile =>
    {
        composeFile.Name = "webdev-workshop-solution";
    });
```

__Note:__ Changing the `Name` property simply changes the name in the Docker-Compose file.

You can also define which resources should be deployed to which publisher, if you have more than one. 

And some publishers also allow you to configure publishing specifics per resource, using extension methods provided by the publisher's NuGet package. The Docker-Compose one is fairly simple though, so there isn't a lot you can do.

However, there is one potentially useful configuration thing you can do. On the `IResourceBuilder<DockerComposeEnvironmentResource>` returned from the call to the `AddDockerComposeEnvironment()`, there is an extension method called `WithProperties()`. This allows you to configure some Docker-Compose specific things. Like for example, if you want to skip the generation of the Docker images during publishing.

```csharp
builder.AddDockerComposeEnvironment("aspire-docker-demo")
    .WithProperties(env =>
    {
        env.BuildContainerImages = false; // skip image build step
    })
    .ConfigureComposeFile(composeFile =>
    {
        composeFile.Name = "webdev-workshop-solution";
    });
```

### Installing the Aspire CLI

Once you have added the publisher to the AppModel, and configured what you need, you need to install the __Aspire CLI__. 

The Aspire CLI can be installed in 2 ways. Either as a native executable, or as a .NET global tool.

To install it as a native executable, you can run the following command to download a PowerShell script

```bash
Invoke-RestMethod https://aspire.dev/install.ps1 -OutFile aspire-install.ps1
```

And then execute the script by calling

```bash
./aspire-install.ps1
```

Optionally, you can do it all in one go by calling

```bash
Invoke-Expression "& { $(Invoke-RestMethod https://aspire.dev/install.ps1) }"
```

__Note:__ This might not run properly. It depends on your PowerShell execution policy.

The other option, to use a global tool, is nice and simple as well. Just run

```bash
dotnet tool install -g Aspire.Cli --prerelease
```

__Note:__ Yes, it is a pre-release. At least at the time of writing.

Once the CLI is installed, you can verify the installation using 

```bash
aspire --version
```

### Performing the publishing

Once the CLI is installed, and you have verified that it works, you can go ahead and publish your solution.

Open a terminal in the directory you have your __WebDevWorkshop.sln__ file, and then simply run

```bash
aspire publish
```

This should output a bit of information as it is working. Hopefully outputting some green checkboxes indicating that it has all worked.

Once the publishing is done, you can have a look at the generated __docker-compose.yaml__ by running

```bash
cat docker-compose.yaml
```

That's "all" there is to publishing your Aspire solutions. Obviously, it depends quite a bit on which publisher you have chosen. But in the end, all you should have to do is run `aspire publish`.

[<< Lab 21](../lab21/lab21.md) | [Home >>](../../readme.md)