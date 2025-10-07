using Microsoft.Extensions.DependencyInjection;

namespace WebDevWorkshop.Services.Products.Client;

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
