# Lab 13: Testing the Shopping Cart Endpoints

The shopping cart isn't very complicated, but it might still be worth testing the endpoints to make sure that they work as expected, and that they don't stop working as expected during future changes.

## Steps (for Visual Studio)

###  Creating a new test project and tests

These test will reside in a new test project. So, go ahead and create a new __xUnit Test Project__ called __WebDevWorkshop.Web.Tests__.

Once the project has been created, you need to add references to both the __WebDevWorkshop.Web__ and __WebDevWorkshop.Testing__ projects.

Then you can rename the __UnitTest1.cs__ to __ShoppingCartTests.cs__, and remove the __Test1__ test.

Inside the `ShoppingCartTests` class, add another public class called __GetShoppingCart__.

__Note:__ This class nesting is only to make the Test Explorer display the tests nicer. And enable potential sharing of code between the test.

Create a new test called __Gets_empty_shopping_cart_by_default__ inside the `GetShoppingCart` class

```csharp
public class ShoppingCartTests
{
    public class GetShoppingCart
    {
        [Fact]
        public Task Gets_empty_shopping_cart_by_default()
        {
        }
    }
}
```

You created this awesome `TestHelper` class in a previous lab. However, that assumes a `DbContext` will be involved. And in this case, that is not true. So, you will need to create another `ExecuteTest` method that does not assume a `DbContext`.

Open the __TestHelper.cs__ file in the __WebDevWorkshop.Testing__ project, and duplicate the `ExecuteTest()` method.

```csharp
public static class TestHelper
{
    public static async Task ExecuteTest<TProgram, TDbContext>(
        Func<DbCommand, Task> dbSetup, 
        Func<HttpClient, Task> test)
        where TProgram : class
        where TDbContext : DbContext
    {
        ...
    }
    
    public static async Task ExecuteTest<TProgram, TDbContext>(
        Func<DbCommand, Task> dbSetup, 
        Func<HttpClient, Task> test)
        where TProgram : class
        where TDbContext : DbContext
    {
        ...
    }
}
```

This new version doesn't need the `TDbContext` type parameter. And because of that, it won't need the `dbSetup` parameter either.

```csharp
public static async Task ExecuteTest<TProgram>( 
    Func<HttpClient, Task> test)
    where TProgram : class
{
    ...
}
```

This change obviously causes the code to stop compiling. Luckily, you can just remove everything that isn't working.

So, go ahead and remove everything inside the call to `builder.ConfigureTestServices()`. As well as everything else that has to do with the database set up. You should be left with something that looks like this

```csharp
public static async Task ExecuteTest<TProgram>(
        Func<HttpClient, Task> test)
        where TProgram : class
{
    var app = new WebApplicationFactory<TProgram>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("IntegrationTesting");
            builder.ConfigureTestServices(services => { });
        });

    var client = app.CreateClient();

    await test(client);
}
```

__Note:__ The call to `ConfigureTestServices()` is left in there even if it is empty, as it will be used later

Now you have a `ExecuteTest` method that you can use for the __WebDevWorkshop.Web__ project, even if it doesn't utilize a `DbContext`.

Open the __ShoppingCartTests.cs__ file again, and go to the `Gets_empty_shopping_cart_by_default` method.

Replace the implementation with a lambda that calls the `TestHelper.ExecuteTest()` method you just created

```csharp
[Fact]
public Task Get_returns_empty_shopping_cart_by_default()
    => TestHelper.ExecuteTest<Program>(async client =>
    {
    });
```

Once again, the `Program` class is causing problems. So, go ahead and add a new public, partial class called __Program__ at the end of the __Program.cs__ file in the __WebDevWorkshop.Web__ project.

```csharp
public partial class Program { }
```

If you go back to the __ShoppingCartTests.cs__ file, the `Program` class should now work as expected.

The test itself only needs to make a GET request to the __/api/shopping-cart__ endpoint, and verify that it returns a successful status code, and an empty JSON array

```csharp
[Fact]
public Task Get_returns_empty_shopping_cart_by_default()
    => TestHelper.ExecuteTest<Program>(async client =>
    {
        var response = await client.GetAsync("/api/shopping-cart");

        response.EnsureSuccessStatusCode();

        var items = JArray.Parse(await response.Content.ReadAsStringAsync());

        Assert.Empty(items);
    });
```

That's all there is to it... Open the Test Explorer and verify that the test is green.

Now, the next test is a bit more complicated, as it will require talking to Orleans.

There are a two ways to solve this. Either you spin up an Orelans test cluster, which is the most "correct" way to do it. Or, you trust Orleans to work, and just fake the `IGrainFactory`. For the sake of simplicity, you will do the latter.

However, to be able to fake a service, you will need to interact with the `TestHelper`. And currently, there is no way to add faked services to it.

Open the __TestHelper.cs__ file and locate the `ExecuteTest()` method you just created. Then add another parameter of the type `Action<IServiceCollection>?`, called __serviceConfig__ to the method. And make it null by default.

```csharp
public static async Task ExecuteTest<TProgram>(
        Func<HttpClient, Task> test,
        Action<IServiceCollection>? serviceConfig = null
)
    where TProgram : class
{
    ...
}
```

Then call that `Func` inside the `ConfigureTestServices()` callback

```csharp
builder.ConfigureTestServices(services =>
{
    serviceConfig?.Invoke(services);
});
```

This allows you to configure whatever services you need in your tests. including faked ones, which is what you need.

Open the __ShoppingCartTests.cs__ file, and add another test called __Gets_shopping_cart_from_grain_if_ShoppingCartId_cookie_exists__ inside the `GetShoppingCart` class

```csharp
public class ShoppingCartTests
{
    public class GetShoppingCart
    {
        ...
        
        [Fact]
        public Task Gets_shopping_cart_from_grain_if_ShoppingCartId_cookie_exists()
        {

        }
    }
}
```

As "usual", the implementation is a call to the `TestHelper`. However, this time there are two parameters to provide

```csharp
[Fact]
public Task Gets_shopping_cart_from_grain_if_ShoppingCartId_cookie_exists()
    => TestHelper.ExecuteTest<Program>(
        serviceConfig: services => {

        },
        test: async client => {
            
        });
```

To fake the `IGrainFactory`, you will need some form of mocking framework. In this lab, this will be one called __FakeItEasy__. So, go ahead and add the NuGet package called __FakeItEasy__.

Then, inside the `serviceConfig`, create a faked `GrainIShoppingCart` instance, and a faked `IGrainFactory` instance to return it based on the grain key __TestCart__. 

Using FakeItEasy, that looks like this

```csharp
serviceConfig: services =>
{
    var grainFake = A.Fake<IShoppingCart>();
    A.CallTo(() => grainFake.GetItems()).Returns([ new ShoppingCartItem {
        ProductId = 1,
        ProductName = "Test Product",
        Count = 1,
        Price = 1.23m
    }]);

    var grainFactoryFake = A.Fake<IGrainFactory>();
    A.CallTo(() => grainFactoryFake.GetGrain<IShoppingCart>("TestCart", null)).Returns(grainFake);
}
```

Finally, add it to the `IServiceCollection` as a __Singleton__

```csharp
serviceConfig: services =>
{
    ...
    services.AddSingleton(grainFactoryFake);
}
```

The test itself is very similar to the previous one, except for one key difference. You need to pass in a __ShoppingCartId__ cookie with the value __TestCart__. And of course verify that the returned data is an array with a single entry that corresponds to the faked `ShoppingCartItem` you just defined.

To set the cookie, you can "simply" set an HTTP header with the name __Cookie__. The value for the cookie is a key value pair string, using a `=` as the delimiter between key and value

```csharp
test: async client =>
{
    client.DefaultRequestHeaders.Add("Cookie", "ShoppingCartId=TestCart");
}
```

With the cookie in place, you can perform the request as usual, and varify the response

```csharp
test: async client =>
{
    client.DefaultRequestHeaders.Add("Cookie", "ShoppingCartId=TestCart");
    var response = await client.GetAsync("/api/shopping-cart");

    response.EnsureSuccessStatusCode();

    Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);

    var shoppingCart = JArray.Parse(await response.Content.ReadAsStringAsync());

    var item = (JObject)Assert.Single(shoppingCart);

    Assert.Equal(1, item["productId"]);
    Assert.Equal("Test Product", item["productName"]);
    Assert.Equal(1, item["count"]);
    Assert.Equal(1.23m, item["price"]);
}
```

Use the Test Explorer to make sure the test is green.

Writing test often becomes very repetitive. But let's add one test to verify that you can write to the cart as well.

But to keep it simple, you can just paste the following as a sibling class next to the `GetShoppingCart` class

```csharp
public class AddShoppingCartItem
{
    [Fact]
    public Task Adds_item_to_grain_with_id_from_ShoppingCartId_cookie()
    {
        var grainFake = A.Fake<IShoppingCart>();

        var grainFactoryFake = A.Fake<IGrainFactory>();
        A.CallTo(() => grainFactoryFake.GetGrain<IShoppingCart>("TestCart", null)).Returns(grainFake);

        var productsClientFake = A.Fake<IProductsClient>();
        A.CallTo(() => productsClientFake.GetProduct(1)).Returns(
            new Product(1, 
                "Test Product", 
                "A Test Product", 
                1.23m, false, 
                "thumbnail.jpg", 
                "image.jpg")
        );

        return TestHelper.ExecuteTest<Program>(
            serviceConfig: services =>
            {
                services.AddSingleton(grainFactoryFake);
                services.AddSingleton(productsClientFake);
            },
            test: async client => {

                client.DefaultRequestHeaders.Add("Cookie", "ShoppingCartId=TestCart");
                var response = await client.PostAsJsonAsync("/api/shopping-cart", 
                                                            new { 
                                                                ProductId = 1, 
                                                                Count = 1 
                                                            });

                response.EnsureSuccessStatusCode();

                A.CallTo(() => grainFake.AddItem(A<ShoppingCartItem>
                                        .That.Matches(x => 
                                                    x.ProductId == 1 
                                                    && x.Price == 1.23m 
                                                    && x.Count == 1)))
                                        .MustHaveHappenedOnceExactly();
            });
    }
}
```

As you can see, this test also needs a faked `IProductsClient`, which is used by the endpoint.

If you want to, you can verify that the test works by using the Test Explorer.

### Cleaning up the tests

Unfortunately, there is quite a lot of repetition in the tests, even when using the `TestHelper`. And in the end, that will be impossible to maintain. So, let's make that a little better by creating a little helper method that removes the repetition for these tests.

At the bottom of the `ShoppingCartTests` class, add a protected, static method called __ExecuteTest__. It should take 2 parameters, a `Func<HttpClient, Task>` called __test__ and an `Action<IGrainFactory, IProductsClient>` called __serviceConfig__. And finally, it needs to return a `Task`.

```csharp
protected static Task ExecuteTest(
              Func<HttpClient, Task> test,
              Action<IGrainFactory, IProductsClient> serviceConfig
            )
{

}
```

This method will replace the call to `TestHelper.ExecuteTest` in the actual tests. Giving the tests a single place to do common set up. In this case, the set up needed is to create the fakes. However, as the fakes need slightly different behavior for each test, the actual behavior config is left up to the `serviceConfig` callback

```csharp
protected static Task ExecuteTest(
              Func<HttpClient, Task> test,
              Action<IGrainFactory, IProductsClient> serviceConfig
            )
{
    var grainFactoryFake = A.Fake<IGrainFactory>();
    var productsClientFake = A.Fake<IProductsClient>();

    serviceConfig(grainFactoryFake, productsClientFake);
}
```

Finally, the method can use the `TestHelper.ExecuteTest` to perform the actual test

```csharp
protected static Task ExecuteTest(
              Func<HttpClient, Task> test,
              Action<IGrainFactory, IProductsClient> serviceConfig
            )
{
    ...
    return TestHelper.ExecuteTest<Program>(
              serviceConfig: services =>
              {
                  services.AddSingleton(grainFactoryFake);
                  services.AddSingleton(productsClientFake);
              },
              test: test);
}
```

So, what does this mean for the tests? Well, they become a lot simpler, with less repetition.

The `Gets_shopping_cart_from_grain_if_ShoppingCartId_cookie_exists` can be updated to look like this

```csharp
public Task Gets_shopping_cart_from_grain_if_ShoppingCartId_cookie_exists()
    => ExecuteTest(
        serviceConfig: (grainFactory, productsClient) =>
        {
            var grainFake = A.Fake<IShoppingCart>();
            A.CallTo(() => grainFake.GetItems()).Returns([ new ShoppingCartItem {
                        ProductId = 1,
                        ProductName = "Test Product",
                        Count = 1,
                        Price = 1.23m
                    }]);

            A.CallTo(() => grainFactory.GetGrain<IShoppingCart>("TestCart", null)).Returns(grainFake);
        },
        test: async client =>
        {
            client.DefaultRequestHeaders.Add("Cookie", "ShoppingCartId=TestCart");
            var response = await client.GetAsync("/api/shopping-cart");

            response.EnsureSuccessStatusCode();

            Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);

            var shoppingCart = JArray.Parse(await response.Content.ReadAsStringAsync());

            var item = (JObject)Assert.Single(shoppingCart);

            Assert.Equal(1, item["productId"]);
            Assert.Equal("Test Product", item["productName"]);
            Assert.Equal(1, item["count"]);
            Assert.Equal(1.23m, item["price"]);
        }
    );
```

And the `Adds_item_to_grain_with_id_from_ShoppingCartId_cookie` one becomes

```csharp
public Task Gets_shopping_cart_from_grain_if_ShoppingCartId_cookie_exists()
{
    var grainFake = A.Fake<IShoppingCart>();

    return ExecuteTest(
        serviceConfig: (grainFactory, productsClient) =>
        {
            A.CallTo(() => grainFactory.GetGrain<IShoppingCart>("TestCart", null)).Returns(grainFake);

            A.CallTo(() => productsClient.GetProduct(1)).Returns(
                new Product(1, 
                    "Test Product", 
                    "A Test Product", 
                    1.23m, false, 
                    "thumbnail.jpg", 
                    "image.jpg")
            );
        },
        test: async client =>
        {
            client.DefaultRequestHeaders.Add("Cookie", "ShoppingCartId=TestCart");
            var response = await client.PostAsJsonAsync("/api/shopping-cart", new { ProductId = 1, Count = 1 });

            response.EnsureSuccessStatusCode();

            A.CallTo(() => grainFake.AddItem(A<ShoppingCartItem>
                                    .That.Matches(x => x.ProductId == 1 && x.Price == 1.23m && x.Count == 1)))
                                    .MustHaveHappenedOnceExactly();
        }
    );
}
```

That is not a huge change, but with a lot of tests, it adds up. And it also makes the tests _very_ precise and easy to read and understand.

[<< Lab 12](../lab12/lab12.md) | [Lab 14 >>](../lab14/lab14.md)