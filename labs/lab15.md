# Lab 15: Adding User Authentication

Now that you have an identity provider in place, it is time to add authentication and authorization to the solution.

## Steps (for Visual Studio)

###  Configuring AuthN/AuthZ

To add authN/authZ to an application using an external identity provier involves redirecting the user to the IdP for authentication. In this case, to the IdentityServer resource you just added. 

To do so, you need to get hold of the URL to the IdP. And as usual with Aspire, that means adding a reference to the target resource. However, in this case, as the target is a container resource, you can't just add a reference as you normally would. Instead, you have to inject the target address manually, using an environment variable.

So, open the __AppHost.cs__ file in the __WebDevWorkshop.AppHost__ project and locate the __webdevworkshop-web__ resource. Then add an environment variable called __IdentityServer__Url__ to the resource. The value should be the __identityserver__ resource's __https__ endpoint. 

```csharp
builder.AddProject<Projects.WebDevWorkshop_Web>(...)
    ...
    .WithEnvironment("IdentityServer__Url", idsrv.GetEndpoint("https"))
    ...;
```

Now that you have access to the URL of the IdP in the __WebDevWorkshop.Web__, you can go ahead and add the required authentication services.

Open the __Program.cs__ file in the __WebDevWorkshop.Web__ project, and add the basic authentication services to the `IServiceCollection` by calling `AddAuthentication()`

```csharp
...
builder.Services.AddAuthentication();

var app = builder.Build();
```

However, this will only add the basic services needed, not the spcific OpenID Connect (OIDC) authentication handler that you need. This is located in a separate NuGet package called __Microsoft.AspNetCore.Authentication.OpenIdConnect__. So, go ahead and add that.

Once that NuGet package has been added, you can configure OIDC authentication using the `AddOpenIdConnect()` extension method on the `AuthenticationBuilder` that is returned from the call to `AddAuthentication()`.

```csharp
...
builder.Services.AddAuthentication()
                .AddOpenIdConnect();
```

The OIDC handle needs a few pieces of configuration to work, and these are configured by passing in a configuration callback to the `AddOpenIdConnect()` method

```csharp
builder.Services.AddAuthentication()
    .AddOpenIdConnect(options =>
    {
        options.Authority = builder.Configuration["IdentityServer:Url"];

        options.ClientId = "interactive.mvc.sample";
        options.ClientSecret = "secret";

        options.ResponseType = "code";
        options.UsePkce = true;

        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;
        options.MapInboundClaims = false;
        options.DisableTelemetry = true;

        options.TokenValidationParameters = new()
        {
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    }););
```

That is a LOT of configuration! Most of it is simply a copy from an [IdentityServer sample](https://github.com/DuendeSoftware/Samples/tree/main/IdentityServer/v7/Basics/MvcBasic). But as mentioned before, the workshop isn't about configuring IdentityServer as such. If you want more information about that, have a look at [Duende Documentation](https://docs.duendesoftware.com/).

But the most important parts are these:

- options.Authority: Where the OIDS IdP is located
- options.ClientId: The OIDC client ID for this applicatiom (hardcoded in the Docker image)
- options.ClientSecret: The secret for this OIDC client (hordcoded as well)
- options.ResponseType: How the IdP should respond. `code` is the recommended approach. You can read more [here](https://curity.io/resources/learn/openid-code-flow/) if you are really interested.
- options.UsePkce: Use Proof Key for Code Exchange is a way for the client to prove it was the one issuing the request. Just turn it on for better security
- options.GetClaimsFromUserInfoEndpoint: By retrieving user information from a separate endpoint, the identity token that is returned from the IdP can be kept smaller, which is how the IdentityServer is configured in this case
- options.SaveTokens: Defines that ASP.NET should store the original token after sign in. This is needed to get sign out to work uninterrupted
- options.MapInboundClaims: Tell ASP.NET Core not to map OIDC-based claims into the Microsoft format
- options.TokenValidationParameters: Tells ASP.NET Core that the users name is in the __name__ claim, and the user's roles are in __role__ claims (Instead of Microsoft's ridiculously long claim names)

Now, the thing is, as soon as the user has been authenticated using OIDC and JWT tokens, the authentication has to be transferred to some other form of authentication. Otherwise you would need to find a way to pass the token back and forth safely. That "other" form, is almost always a Cookie. So you also need to add cookie authentication to the authentication setup

```csharp
builder.Services.AddAuthentication()
    .AddOpenIdConnect(options => ...)
    .AddCookie();
```

However, now that you have 2 different authentication mechanisms, you have to tell the authentication system when to use which. This is done in a configuration callback passed to the `AddAuthentication()` method.

In this case you want to use cookie authentication as the default scheme, that is the one used to authenticate the user by default. But use OIDC when sending a chalenge to have the user log in.

```csharp
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddOpenIdConnect(options => ...)
    .AddCookie();
```

__Note:__ There is a convention that says that every authentication handler should expose its default name as a constant named __AuthenticationScheme__, in a static "Defaults" class. So, for the cookie auth, that is `CookieAuthenticationDefaults.AuthenticationScheme`, and for the OIDC auth, that is `OpenIdConnectDefaults.AuthenticationScheme`

__Comment:__ You can change the name used to identify the scheme if you want to. But these are the names used unless you explicitly provide a different one while registering the handler.

Now that all the required authentication services and handler have been added, you need to add the required middlewares to the request pipeline as well. They way to do that, is to call `UseAuthentication()` and `UseAuthorization`.

```csharp
...
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
...
```

__Note:__ The placement of these calls do matter. They should be placed right after `UseRouting()` in this case. And anything put in the pipeline before these calls would not have access to any potentially authenticated user.

### Adding the auth endpoints used by the UI

The UI expects 3 endpoints related to authentication, __/api/me__, __auth/login__ and __/auth/logout__. The latter 2 are probably self explanatory. However, the first one might not be.

The __/api/me__ endpoint is called by the React front-end on load to figure out if the user is logged in, and if so, what's the name of the logged in user.

Let's start with implementing that one!

Map a new minimal API GET endpoint for that path right before the POST endpoint for __/api/shopping-cart__.

```csharp
...
app.MapGet("/api/me", () => {}
);

app.MapPost("/api/shopping-cart", ...);
...
```

The endpoint needs to know if there is a currently logged in user. The weird, but still somewhat logical way to do this, is to request to get a `ClaimsPrincipal` injected into the handler

```csharp
app.MapGet("/api/me", (ClaimsPrincipal user) => {});
```

The endpoint should return an HTTP 200 and the user's name

```csharp
app.MapGet("/api/me", (ClaimsPrincipal user) =>  
    Results.Ok(user.Identity!.Name)
);
```

But what if there is no authenticated user? Well, then it should return 401 Unauthorized. Now, instead of checking if the user is authenticated own your own. You can simply require the user to be authenticated when calling this endpoint. That will have the same effect

```csharp
app.MapGet("/api/me", (ClaimsPrincipal user) => 
    Results.Ok(user.Identity!.Name)
).RequireAuthorization();
```

The other endpoints are a bit more complex, so it might be easier to use an MVC controller for those.

Add a new, empty MVC controller called __AuthController__ in the __Controllers__ directory, and addorn it with a `Route` attribute with the value __[controller]__ to make all actions inside it have their paths prefixed with __auth__. You can also remove the auto-generated `Index()` action, as it won't be needed

```csharp
[Route("[controller]")]
public class AuthController() : Controller
{
}
```

In the new controller, add an action called __LogIn__. It should take an optional `string` parameter called __returnUrl__, with a default value of __/__. Also add a `HttpGet` attribute to make it respond to the path __/auth/login__

```csharp
[HttpGet("login")]
public IActionResult LogIn(string returnUrl = "/")
{
}
```

This endpoint should send a challenge to the user to log in. However, before you do that, you really should verify that the `returnUrl` is a local URL. Otherwise there are some potentially serious security implications. If it isn't a local URL, just make it local


```csharp
[HttpGet("login")]
public IActionResult LogIn(string returnUrl = "/")
{
    if (!Url.IsLocalUrl(returnUrl))
        returnUrl = "/";
}
```

Once you know the `returnUrl` is fine, you can return a challenge to the user, using the `Challenge()` method in the base class. Don't forget to set the return URL though. Otherwise it will return the user to this endpoint after log in, causing an infinite loop.

```csharp
[HttpGet("login")]
public IActionResult LogIn(string returnUrl = "/")
{
    if (!Url.IsLocalUrl(returnUrl))
        returnUrl = "/";

    return Challenge(new AuthenticationProperties { 
            RedirectUri = returnUrl 
        });
}
```

That's it! Now the user has an endpoint to call when he or she wants to log in. The only thing now is to enable loggin out.

Add one more action. This time it should be called __LogOut__, take no input, respond to __/auth/logout__ and asynchronously return `Task` instead of `IActionResult`

```csharp
[HttpGet("logout")]
public async Task LogOut()
{
}
```

In this action you need to do 2 things. First of all, you need to sign out the user from the cookie based authentication. This will basically just remove the authentication cookie. Secondly, you need to tell it to sign out the user from the OIDC provider as well. Otherwise the user will just be logged back in again automatically if ever asked to login again.

To sign out the user from the cookie authentication is simple. As it is the default authentication scheme, you just need to await a call to `HttpContext.SignOutAsync()`. For the OIDC, you need to specify that this is the one you want to sign out from, as it isn't the default authentication scheme. Once again, this is done using by awaiting a call to `HttpContext.SignOutAsync()`, but you need to provide it with the name of the scheme to sign out from.

```csharp
[HttpGet("logout")]
public async Task LogOut()
{
    await HttpContext.SignOutAsync();
    await HttpContext.SignOutAsync(
        OpenIdConnectDefaults.AuthenticationScheme
    );
}
```

### Verify that it works

Press __F5__ to start debugging, and then click on the log in button in the top right corner of the website

__Note:__ Yes, that is a sign out icon, but there wasn't a better one to use for log in at the time of the creation of the front-end... 😂

Unfortunately it fails... So, open the logs for the __identityserver__ resource. You should see a message that says __Invalid redirect__. This is because the address that the user is to be redirected to after logging in must be whitelisted. In this case, the IdentityServer expects the user to be redirected to __https://localhost:7278__, which is different than the address that the user is trying to get redirected to.

To fix that, you need to move the __webdevworkshop-web__ resource to port __7278__. The easiest way to do that, is to open the __Properties/launchSettings.json__ file in the __WebDevWorkshop.Web__ project, and update the `applicationUrl` value for the __aspire__ profile.

```json
"aspire": {
    ...
    "applicationUrl": "https://localhost:7278",
    ...
}
```

Restart the debuggin and try loggin in again. The username is __alice__ and the password is also __alice__.

This should now succeed. And you should also be able to sign out.

__Note:__ Only the sign in and sign out buttons are enabled in the UI. The others are just for show... 😂

[<< Lab 14](./lab14.md) | [Lab 16 >>](./lab16.md)