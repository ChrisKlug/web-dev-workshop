# Lab 14: Adding an IdentityServer

As setting up an identity provider isn't the main focus of this workshop, you will use a pre-created Docker image that has a pre-configured IdentityServer inside it.

__Note:__ If you are curious about Duende IdentityServer, you can find out everything you need at [Duende](https://duendesoftware.com/products/identityserver)

## Steps (for Visual Studio)

###  Adding IdentityServer

Open the __AppHost.cs__ file in the __WebDevWorkshop.AppHost__ project, and add a new container resource somewhere before the addition of the __webdevworkshop-web__ resource.

The name should be __identityserver__, and the image should be __zerokoll/webdevworkshop-identity-server__. Just as with the __ui__ resource, you will need to configure the required ports. In this case, there is only one port that needs to be exposed, and that is port 8081. You can let Aspire control the port it chooses on the host.

Don't forget to store the resource so that you can use it as a reference later on

```csharp
var idsrv = builder.AddContainer("identityserver", "zerokoll/webdevworkshop-identity-server")		      
    .WithHttpsEndpoint(targetPort: 8081);
```

And just for the sake of it, you can add a call to `WithExternalHttpEndpoints()` as a good measure, as the identity provider needs to be publicly available. 

```csharp
var idsrv = builder.AddContainer(...)		      
    .WithHttpsEndpoint(targetPort: 8081)
    .WithExternalHttpEndpoints();
```

It won't do anything when running locally, but during a deployment it makes a big difference. So, it might be a good practice to call it in case you ever need to deploy the solution.

### Verify that the IdentityServer works

Now that you have added the container, it might be a good idea to see if it comes online.

Press __F5__ to start debugging.

You should see a new resource called __identityserver__ come online after a little while.

It might take a few seconds, as it needs to download the Docker image first.

Once it comes online, click on the URL to see if it responds.

It doesn't, unfortunately.

If you go back to the Aspire Dashboard, and open the logs for the __identityserver__ resource, you will see a line that says __Now listening on: http://[::]:8080__.

That says HTTP, not HTTPS, as the resource has been configured to use.

Now, you could just change it over to HTTP, but considering that we are talking about authentication, that might be a really bad idea. Instead, let's reconfigure it to use HTTPS.

Go back to the __AppHost.cs__ file and the __identityserver__ resource.

ASP.NET Core defines what ports and schemes it should listen to using, among other things, an environment variable called __ASPNETCORE_URLS__. So, by setting that to __https://*:8081__, we can tell the IdentityServer to listen to HTTPS.

```csharp
var idsrv = builder.AddContainer("identityserver", "zerokoll/webdevworkshop-identity-server")		      
    .WithHttpsEndpoint(targetPort: 8081)
    .WithEnvironment("ASPNETCORE_URLS", "https://*:8081")
    .WithExternalHttpEndpoints();
```

Now, press __F5__ and see what happens!

Oops... Now the resource comes up in an __Exited__ state.

Open the logs again, and you will now see __Unable to configure HTTPS endpoint. No server certificate was specified...__.

Ok, there is no certificate inside the container that it can use. Or at least not one that it can find.

### Adding HTTPS support to the Identity Server container

To be able to support HTTPS, you will need a TLS certificate to work with. Luckily, ASP.NET Core creates one of those for you the first time you run an ASP.NET Core application. So, let's use that!

First, you need to export the certificate as a __pfx__ file. To do this, open the Start menu and find the __Manage User Certificates__ entry. 

Go to __Personal__ > __Certificates__ and locate the certificate with a friendly name of __ASP.NET Core HTTPS development certificate__. 

Right-click it, and choose __All Tasks__ > __Export...__. 

Then click __Next__. 

Choose __Yes, export the private key__, as this will be needed to use it to encrypt the communication. 

Click __Next__. 

Choose __Personal Information Exchange - PKCS #12 (.PFX)__

Click __Next__. 

Tick __Password__ and add a secure password like __P@ssw0rd123!__

Confirm the password.

Click __Next__. 

Save it to a root of your development project, naming it __ssl-cert.pfx__

Close the __certmgr__ window.

__Important:__ This certificate is trusted by your machine, so make sure it doesn't leave your machine. After the workshop, it is recommended to delete the file completely.

Now that you have the certificate, we can add it to the IdentityServer container using a bind mount.

Open the __AppHost.cs__ file, and locate the __identityserver__ resource. Use the `WithBindMount()` method to bind the newly created PFX file to __/devcert/ssl-cert.pfx__. And make it readonly for good measure...

```csharp
var idsrv = builder.AddContainer(...)		      
    ...
    .WithBindMount(
        Path.Combine(builder.Environment.ContentRootPath, "../ssl-cert.pfx"), 
        "/devcert/ssl-cert.pfx", 
        true)
    ...
```

The certificate is now available inside the container. But we still need to tell Kestrel to use it.

Once again, environment variables come to the rescue. In this case it is the __Kestrel__Certificates__Default__Path__ and __Kestrel__Certificates__Default__Password__ that allows us to configure it.

The __Kestrel__Certificates__Default__Path__ is easy; you just need to set it like this:

```csharp
var idsrv = builder.AddContainer(...)		      
    ...
    .WithEnvironment("Kestrel__Certificates__Default__Path", "/devcert/ssl-cert.pfx")
    ...
```

The __Kestrel__Certificates__Default__Password__ on the other hand is more complicated. You probably don't want to put your password into the code. Instead you can rely on a `ParameterResource`, which is an Aspire way to handle configuration.

You just need to define a `ParameterResource` for the password like this

```csharp
var sslPasswordParameter = builder.AddParameter("ssl-password", true);

var idsrv = builder.AddContainer(...);
```

__Note:__ The second parameter (`true`) makes the parameter a secret, so it isn't added to logs etc

Then you can pass it to the `WithEnvironment()` call like this

```csharp
var idsrv = builder.AddContainer(...)		      
    ...
    .WithEnvironment("Kestrel__Certificates__Default__Password", sslPasswordParameter)
    ...
```

This would work, but we can make it a little better. First of all, you can make the `sslPasswordParameter` a child resource of the `idsrv` by writing

```csharp
var idsrv = builder.AddContainer(...);

sslPasswordParameter.WithParentRelationship(idsrv);
```

Now, if you don't provide a value for the parameter, Aspire will ask you for one when it starts up. To make that experience a little better, we can include some metadata for the `sslPasswordParameter`.

```csharp
var sslPasswordParameter = builder.AddParameter("ssl-password", true)
    .WithCustomInput(parameter => new()
    {
        Name = parameter.Name,
        Label = "SSL Certificate Password",
        Placeholder = "The password",
        InputType = InputType.SecretText,
        Required = true
    });
```

Unfortunately, at the time of writing, this is experimental code, so you need to disable a warning using a pragma for it to compile.

```csharp
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
```

__Note:__ Please let Chris know if this is not experimental anymore

### Verify that HTTPS works

Press __F5__ to start debugging the solution.

As the Aspire Dashboard opens up, you will get a big warning at the top saying __Unresolved parameters__, and the __identityserver__ resource will not start.

To the right, in the error pane, click the __Enter values__ button.

In the modal that pops up, you will see the missing parameter. In this case, the SSL Certificate Password. And you will also see that it is using the label and placeholder you defined.

Add your password to the input, and tick the __Save to user secrets__ checkbox, before clicking __Save__.

Once you have added the password, the __identityserver__ resource should come online.

And if you click on the URL to the __identityserver__, you should be greeted by a __Duende IdentityServer__ page.

__Note:__ By ticking the __Save to user secrets__ box, Aspire has automatically saved the value in your user secrets for this project. So, in the future you will not be asked for a password.

[<< Lab 13](../lab13/lab13.md) | [Lab 15 >>](../lab15/lab15.md)