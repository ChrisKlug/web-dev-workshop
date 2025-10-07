using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using WebDevWorkshop.Services.Products.Data;

namespace WebDevWorkshop.Services.Products.Endpoints
{
    public class ProductEndpoint(IProducts products) : Endpoint< ProductEndpoint.Request, Results<Ok<Product>, NotFound>>
    {
        public record Request(int Id);

        public override void Configure()
        {
            Get("/api/products/{id}");
            AllowAnonymous();
        }

        public override async Task<Results<Ok<Product>, NotFound>> ExecuteAsync(Request req, CancellationToken ct)
        {
            var product = await products.WithId(req.Id);

            return product is not null
                    ? TypedResults.Ok(product)
                    : TypedResults.NotFound();
        }
    }
}
