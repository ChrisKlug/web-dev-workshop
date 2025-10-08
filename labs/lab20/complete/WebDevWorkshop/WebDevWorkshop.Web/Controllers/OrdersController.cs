using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebDevWorkshop.Services.Orders.gRPC;
using WebDevWorkshop.Services.Products.Client;
using WebDevWorkshop.Web.Models;
using WebDevWorkshop.Web.ShoppingCart;

namespace WebDevWorkshop.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OrdersController(IProductsClient productsClient,
                                OrdersService.OrdersServiceClient ordersService,
                                IGrainFactory grainFactory) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> AddOrder(AddOrderModel order)
    {
        var request = new AddOrderRequest
        {
            DeliveryAddress = new Address
            {
                Name = order.DeliveryAddress.Name,
                Street1 = order.DeliveryAddress.Street1,
                Street2 = order.DeliveryAddress.Street2 ?? "",
                PostalCode = order.DeliveryAddress.PostalCode,
                City = order.DeliveryAddress.City,
                Country = order.DeliveryAddress.Country,
            },
            BillingAddress = new Address
            {
                Name = order.BillingAddress.Name,
                Street1 = order.BillingAddress.Street1,
                Street2 = order.BillingAddress.Street2 ?? "",
                PostalCode = order.BillingAddress.PostalCode,
                City = order.BillingAddress.City,
                Country = order.BillingAddress.Country,
            }
        };

        var retrievalTasks = order.Items.Select(x =>
            productsClient.GetProduct(x.ItemId)
            ).ToArray();

        try
        {
            await Task.WhenAll(retrievalTasks);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Something went wrong...");
        }

        var retrievedProducts = retrievalTasks
                        .Select(x => x.Result!)
                        .ToDictionary(x => x.Id, x => x);

        foreach (var item in order.Items)
        {
            var product = retrievedProducts[item.ItemId];
            request.Items.Add(new OrderItem
            {
                Name = product.Name,
                Price = (float)product.Price,
                Quantity = item.Quantity
            });
        }

        AddOrderResponse response;
        try
        {
            response = await ordersService.AddOrderAsync(request);
            var shoppingCart = grainFactory.GetGrain<IShoppingCart>(
                Request.Cookies["ShoppingCartId"]
            );
            await shoppingCart.Clear();
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Something went wrong...");
        }

        if (!response.Success)
        {
            return StatusCode(500, "Something went wrong...");
        }

        return Ok(new { response.Success, response.OrderId });

    }
}
