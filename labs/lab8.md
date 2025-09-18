# Lab 8: Testing the Product Endpoint

You now have a, at least very basic, test for the `FeaturedProductsEndpoint` endpoint. But you still have one endpoint to test, the `ProductEndpoint` one.

Luckily, with the newly created `TestHelper`, this is very easy to do.

## Steps (for Visual Studio)

### Create a new test class and test

Create a new class called __ProductEndpointTests__ in the __WebDevWorkshop.Services.Products.Tests__ project.

__Note:__ Make sure the class is public, otherwise the test runner can't find it

Inside the class, add a test method called __GET_Returns_HTTP_200_and_the_requested_product__

```csharp
[Fact]
public Task GET_Returns_HTTP_200_and_the_requested_product()
{
    
}
```

In this case, as you will need to use the id of the product you are adding to the database as part of the setup, you can't implement as a simple lambda function. Instead, the call to the `TestHelper.ExecuteTest()` method needs to be inside a proper method

```csharp
[Fact]
public Task GET_Returns_HTTP_200_and_the_requested_product()
{
    return TestHelper.ExecuteTest<Program, ProductsContext>(
        dbSetup: async cmd =>
        {
            
        },
        test: async client =>
        {

        }
    );
}
```

__Note:__ This will make a lot more sense in a second...

In the database setup, you need to add a product to return in the API. A task that is easily solved by using the `AddProduct` extension method you created in the previous lab.

However, you also need to store the returned id so that you can access it from the `test` func. This is why you need to use a block bodied function, as this allows you to define variables outside of the call to `TestHelper.ExecuteTest()`

```csharp
var productId = 0;

return TestHelper.ExecuteTest<Program, ProductsContext>(
    dbSetup: async cmd =>
    {
        productId = await cmd.AddProduct("Product 1", "Description 1", 
            100m, true, "product1");

    },
    ...
);
```

The actual test implementation is just as simple as it was in the previous test. Just use the `client` to make a __GET__ request to the __/api/products/{id}__ endpoint, and then assert that the returned value looks like you expect it to

```csharp
return TestHelper.ExecuteTest<Program, ProductsContext>(
    ...,
    test: async client =>
    {
        var response = await client.GetAsync($"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        dynamic json = JObject.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(productId, (int)json.id);
        Assert.Equal("Product 1", (string)json.name);
        Assert.Equal("Description 1", (string)json.description);
        Assert.Equal(100m, (decimal)json.price);
        Assert.True((bool)json.isFeatured);
        Assert.Equal("product1_thumbnail.jpg", (string)json.thumbnailUrl);
        Assert.Equal("product1.jpg", (string)json.imageUrl);
    }
);
```

That's it!

### Verify that it works

Pull up the Test Explorer and run all tests again! 

It should come back green for both tests!

__Note:__ You might need to re-build the solution to get the Test Explorer to see the new test

[<< Lab 7](./lab7.md) | [Lab 9 >>](./lab9.md)