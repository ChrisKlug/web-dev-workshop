using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WebDevWorkshop.Services.Products.Client;

namespace WebDevWorkshop.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductsController(IProductsClient productsClient) : ControllerBase
{
    [HttpGet("featured")]
    public async Task<Ok<Product[]>> GetFeaturedProducts()
        => TypedResults.Ok(
            await productsClient.GetFeaturedProducts()
        );

    [HttpGet("{productId}")]
    public async Task<Results<NotFound, Ok<Product>>> GetProduct(int productId)
    {
        var product = await productsClient.GetProduct(productId);
        return product is not null
            ? TypedResults.Ok(product)
            : TypedResults.NotFound();
    }
}
