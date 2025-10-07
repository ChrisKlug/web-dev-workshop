using WebDevWorkshop.Services.Products.Client;

namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddProductsClient(this IServiceCollection services, string baseAddress)
    {
        services.AddHttpClient<IProductsClient, HttpProductsClient>(client => {
            client.BaseAddress = new Uri(baseAddress);
        });
        return services;
    }
}
