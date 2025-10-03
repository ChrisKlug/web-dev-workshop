using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using WebDevWorkshop.Services.Products.Data;

namespace WebDevWorkshop.Services.Products.Endpoints
{
    public class FeaturedProductsEndpoint(IProducts products) : EndpointWithoutRequest<Ok<Product[]>>
    {
        public override void Configure()
        {
            Get("/api/products/featured");
            AllowAnonymous();
        }

        public override async Task<Ok<Product[]>> ExecuteAsync(CancellationToken ct)
            => TypedResults.Ok(await products.ThatAreFeatured());
    }
}
