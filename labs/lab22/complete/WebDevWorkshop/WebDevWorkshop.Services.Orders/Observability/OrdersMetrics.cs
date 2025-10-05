using System.Diagnostics.Metrics;

namespace WebDevWorkshop.Services.Orders.Observability
{
    public class OrdersMetrics
    {
        public const string MeterName = "WebDevWorkshop.Services.Orders";

        public OrdersMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create(MeterName);
            TotalOrdersCounter = meter.CreateCounter<int>("total_orders");
        }

        public void AddOrder() => TotalOrdersCounter.Add(1);

        private Counter<int> TotalOrdersCounter { get; }
    }
}
