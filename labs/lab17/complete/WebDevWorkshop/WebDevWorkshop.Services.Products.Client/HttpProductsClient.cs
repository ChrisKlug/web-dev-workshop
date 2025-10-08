﻿using System.Net.Http.Json;
using System.Text.Json;

namespace WebDevWorkshop.Services.Products.Client;

internal class HttpProductsClient(HttpClient httpClient) : IProductsClient
{
    public async Task<Product?> GetProduct(int productId)
    {
        var response = await httpClient.GetAsync("api/products/" + productId);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        return JsonSerializer.Deserialize<Product>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }

    public Task<Product[]> GetFeaturedProducts()
        => httpClient.GetFromJsonAsync<Product[]>("/api/products/featured")!;
}
