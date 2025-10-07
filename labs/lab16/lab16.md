# [Optional] Lab 16: Testing with Authentication

The thing is, now that you have added authentication to the website, you probably want to test it as well. But it doesn't really matter if you want to test it or not, because if you open the Test Explorer and run the tests, you will actually notice that they are failing at the moment.

If you dig into the tests and look at the error, you will see that it throws an __InvalidOperationException__ with the message __Provide Authority, MetadataAddress, Configuration, or ConfigurationManager to OpenIdConnectOptions__.


This is a bit confusing if you don't know what it means. But it means that the address of the IdP hasn't been provided. And because of that, the OIDC handler can't find the information that it needs to do its work.


There is a two-part solution to this. The first part is to remove the OIDC authentication scheme to make it not fail. The second part is to add another authentication scheme to use during testing.

## Steps (for Visual Studio)

###  Part 1 - Removing the OIDC scheme

Unfortunately, there doesn't seem to be a nice way to remove the authentication handler from the test setup, so it has to be done in a slightly brutish way.

As you have configured the environment name to be different during tests, you can use this information to only add the OIDC scheme if the application is not in test.

Update the current code that looks like this

```csharp
builder.Services.AddAuthentication(...)
    .AddOpenIdConnect(...)
    .AddCookie();
```

to a code that only adds the OIDC scheme if the environment name isn't __IntegrationTesting__

```csharp
var auth = builder.Services.AddAuthentication(...)
    .AddCookie();

if (!builder.Environment.IsEnvironment("IntegrationTesting"))
{
    auth.AddOpenIdConnect(...);
}
```

It's not an elegant solution, but it works...

You should now be able to run the tests and get a green light back!

###  Part 2 - Add Basic authentication during testing

Now, even if the tests are ok, you aren't really testing the application like it would be used in production. This is not a good thing...

The ASP.NET Core team has spent a lot of time getting the authentication to work. And because of this, we should be able to trust that it works. They also made the authentication handler a pluggable piece. So, plugging in a different handler should not affect the application in any other way than the way that the user is authenticated. 

You could plug in an OIDC handler using manual configuration. However, creating JWT-tokens for the tests is a bit of a pain. And, since the ASP.NET Core teams spent all that time creating a pluggable system, replacing OIDC with another authentication implementation should be fine. It should still verify that the application handles auth properly, even if the actual authentication step is different.

So, during tests, it is much easier to simply replace OIDC with Basic Auth.

To simplify the set up of the Basic Auth during tests, you can create an extension method that extends `IServiceCollection`.

In the __WebDevWorkshop.Testing__ project, add a new class called __IServiceCollectionExtensions__. Make the class `internal`, and `static` so that you can use it for extension methods.

```csharp
internal static class IServiceCollectionExtensions
{
    
}
```

In the newly created class, add an extension method called __AddTestAuthentication__. It should extend `IServiceCollection`, and return `IServiceCollection`.

```csharp
public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
{
    return services;
}
```

The first step in here is to call `AddAuthentication()` to get hold of the `AuthenticationBuilder`.

```csharp
public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
{
    services.AddAuthentication();
    
    return services;
}
```

Next you need to add a basic authentication scheme. And for this, you need a NuGet package called __Bazinga.AspNetCore.Authentication.Basic__.

With that package in place, you can call `AddBasicAuthentication()` to add the basic auth scheme. 

The `AddBasicAuthentication()` method has a few overloads, but the one you need to use takes a callback that takes a parameter of the type `(string username, string password)`, and returns a `Task<bool>`. In this callback, you need to verify that the used credentials are correct.

For testing, this verification can be really simple. You just need to verify that both the username and password is __test__.

```csharp
services.AddAuthentication()
        .AddBasicAuthentication(creds => 
            Task.FromResult(
                creds.username.Equals("test", StringComparison.InvariantCultureIgnoreCase)
                && creds.password == "test"
            )
        );
```

__Note:__ Yes, this code uses a case-insensitive check for the username. This isn't really necessary, but conforms to standards...

Now that you have added the basic auth scheme, you also need to update the authentication defaults. Currently, the application sets cookies to be the default scheme, but OIDC to be the default for challenging the user. That is obviously not going to work in the tests, where there is no OIDC scheme. 

So, to reconfigure that, update the `AddAuthentication()` call to set the basic auth scheme as the default for both

```csharp
services.AddAuthentication(options => {
            options.DefaultScheme = BasicAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = BasicAuthenticationDefaults.AuthenticationScheme;
        })
        ...;
```

Ok, that's it for the extension method. Using it should now set up basic auth as the authentication scheme.

Now, you just need to update the `TestHelper` to use it.

Open the __TestHelper.cs__ file, and locate the second "simpler" `ExecuteTest` method. 

__Important!__ If you did no do lab number 13, which was optional, you will need to add the second "simpler" `ExecuteTest` method now, as it will be missing. Just copy the following code into the __TestHelper.cs__ file

```csharp
public static async Task ExecuteTest<TProgram>(
        Func<HttpClient, Task> test,
        Action<IServiceCollection>? serviceConfig = null
    )
        where TProgram : class
    {
        var app = new WebApplicationFactory<TProgram>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("IntegrationTesting");
                builder.ConfigureTestServices(services =>
                {
                    serviceConfig?.Invoke(services);
                });
            });

        var client = app.CreateClient();

        await test(client);
    }
```

If you already did lab 13, you will already have created this method.

You need to add the call to the `AddTestAuthentication()` method in this "simpler" one method, as the other one is used by the products service that doesn't use authentication at all.

Inside the `ConfigureTestServices()` call, after the call to the `serviceConfig`, add a call to the newly added extension method.

```csharp
builder.ConfigureTestServices(services =>
{
    serviceConfig?.Invoke(services);

    services.AddTestAuthentication();
});
```

### Verifying that it works

With the authentication in place, you can open the Test Explorer and run the tests.

They should come back green at this point. However, that is only kind of true... As you are currently only testing unauthenticated endpoints, you don't actually know if the authentication is working.

### Testing the /api/me endpoint

The only endpoint that supports authentication at the moment, is the __/api/me__ endpoint. So, let's test that.

Add a new class called __MeTests__ in the __WebDevWorkshop.Web.Tests__ project. And make sure the class is `public`.

The first test should verify that the API returns 401 Unauthorized if the user isn't logged in.

Create a new test called __Get_returns_HTTP_401_Unauthorized_if_not_authenticated__, and have it call the `TestHelper`

```csharp
[Fact]
public Task Get_returns_HTTP_401_Unauthorized_if_not_authenticated()
=> TestHelper.ExecuteTest<Program>(
              test: async client => {
              }
    );
```

The actual test code is really simple. Just call the endpoint, and verify that you get a 401 back.


```csharp
[Fact]
test: async client => {
    var response = await client.GetAsync("/api/me");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}
```

Open the Test Explorer and run the test to make sure it passes.

This confirms that authentication is working. But, you also need to verify that the endpoint works with an authenticated user.

So, create another test called __Get_returns_HTTP_200_OK_and_name_if_authenticated__. It should do the same thing as the previous test, but assert that the returned status code is 200 OK, and that the payload is the logged-in user's name.

```csharp
[Fact]
public Task Get_returns_HTTP_200_OK_and_name_if_authenticated()
    => TestHelper.ExecuteTest<Program>(
        test: async client => {
            var response = await client.GetAsync("/api/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("\"test\"", await response.Content.ReadAsStringAsync());
        }
    );
```

__Note:__ The content is compared to `"test"` as the returned string is converted to JSON, which wraps it in `"` characters.

If you run the test, it will come back as red. The reason is obviously because there is no authenticated user called __test__. 

The way to fix that is to pass in a basic auth header to the endpoint. But, instead of doing that in the test, you can configure the `TestHelper` to do it for you. And as most tests will want to have a logged-in user, but not all, you need to make it configurable.

Open the __TestHelper.cs__ file in the __WebDevWorkshop.Testing__ project, and locate the second, simpler `ExecuteTest()` method again. 

Update the method signature by adding another parameter called __isAuthenticated__. It should be of type `bool`, and have a default value of `true`.

__Note:__ Making the default `true` is because, as mentioned before, most test will want to run as if authenticated.

```csharp
public static async Task ExecuteTest<TProgram>(
        Func<HttpClient, Task> test,
        Action<IServiceCollection>? serviceConfig = null, 
        bool isAuthenticated = true
    )
    ...
```

Then, inside the method, right after the creation of the `HttpClient`, you need to add the actual authentication. But only if the `isAuthenticated` is `true`.

Basic Auth is performed by passing in the username and password using a header called __Authorization__. The credentials need to be concatenated with `:` separator, and then base64 encoded before being added to the header.

It should looks something like this

```csharp
var client = app.CreateClient();
        
if (isAuthenticated)
{
    var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.UTF8.GetBytes("test:test"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
}

await test(client);
```

This causes a small problem though. At the same time as it hopefully makes the authenticated test work, it breaks the unauthenticated one. So, you will need to update the `Get_returns_HTTP_401_Unauthorized_if_not_authenticated` test to set the `isAuthenticated` parameter to `false`.

```csharp
public Task Get_returns_HTTP_401_Unauthorized_if_not_authenticated()
        => TestHelper.ExecuteTest<Program>(
            isAuthenticated: false,
            test: ...
        );
```

Once that is fixed, you can open the Test Explorer and run the "entire" test suite to make sure everything is green.

### Returning 401 from API endpoints

Unfortunately, there is one small issue still...

The tests, using basic auth, verify that the __/api/me__ endpoint returns 401 Unauthorized, which is what we expect. However, it does so because basic auth doesn't support challenging the user to log in. OIDC does. So, what does that mean? Well...

Press __F5__ to start debugging. Once the website opens up, make sure you are logged out. And then browse to __/api/me__.

As you can see, you are redirected to the IdP instead of being served a 401. 

To make the endpoint return 401, you need to make a small tweak to the OIDC handler registration.

Open the __Program.cs__ file in the __WebDevWorkshop.Web__ project, and locate the code that registers the OIDC scheme. 

At the end of the callback used in the call to the `AddOpenIdConnect()` method, add a callback for the __OnRedirectToIdentityProvider__ event.

__Note:__ There are several authentication related events exposed through the `OpenIdConnectOptions.Events` property. These can be really powerful when you need to do something custom, or simply just for debugging when stuff isn't working.

```csharp
auth.AddOpenIdConnect(options =>
{
    ...
    
    options.Events.OnRedirectToIdentityProvider = ctx =>
    {
        return Task.CompletedTask;
    };
});
```

In the callback, you want to verify that the requested path starts with __/api__, as you only want these calls to get a 401. All other calls should challenge the user to log in using a redirect.

If it is a request to __/api/*__, you should set the `StatusCode` property to 401 on the `HttpResponse`. And then you need to call `ctx.HandleResponse();` to tell the system that you have handled it and no other code should mess with it.

```csharp
options.Events.OnRedirectToIdentityProvider = ctx =>
{
    if (ctx.HttpContext.Request.Path.StartsWithSegments("/api"))
    {
        ctx.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        ctx.HandleResponse();
    }
    return Task.CompletedTask;
};
```

__Note:__ The code uses `(int)HttpStatusCode.Unauthorized` instead of just `401`. The reason for this is simply that it is easier to read and understand what it does. Not everyone knows all the HTTP status codes by heart.

If you press __F5__ now, and then browse to __/api/me__, you should get an HTTP 401 instead of a redirect.

Mission accomplished!

[<< Lab 15](../lab15/lab15.md) | [Lab 17 >>](../lab17/lab17.md)