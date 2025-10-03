using Grpc.Core;
using WebDevWorkshop.Services.Orders.Data;
using WebDevWorkshop.Services.Orders.Entities;
using WebDevWorkshop.Services.Orders.gRPC;

namespace WebDevWorkshop.Services.Orders.Services
{
    public class OrdersService(OrdersContext dbContext, ILogger<OrdersService> logger) 
        : gRPC.OrdersService.OrdersServiceBase
    {
        public override async Task<AddOrderResponse> AddOrder(AddOrderRequest request, ServerCallContext context)
        {
            var deliveryAddress = DeliveryAddress.Create(request.DeliveryAddress.Name,
                                                        request.DeliveryAddress.Street1,
                                                        request.DeliveryAddress.Street2,
                                                        request.DeliveryAddress.PostalCode,
                                                        request.DeliveryAddress.City,
                                                        request.DeliveryAddress.Country);

            var billingAddress = BillingAddress.Create(request.BillingAddress.Name,
                                                        request.BillingAddress.Street1,
                                                        request.BillingAddress.Street2,
                                                        request.BillingAddress.PostalCode,
                                                        request.BillingAddress.City,
                                                        request.BillingAddress.Country);

            var order = Order.Create(deliveryAddress, billingAddress);

            foreach (var item in request.Items)
            {
                order.AddItem(item.Name, item.Quantity, (decimal)item.Price);
            }

            dbContext.Add(order);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding order {orderId}", order.OrderId);
                return new AddOrderResponse
                {
                    Success = false,
                    Error = ex.GetBaseException().Message
                };
            }

            return new AddOrderResponse
            {
                Success = true,
                OrderId = order.OrderId
            };
        }
    }
}
