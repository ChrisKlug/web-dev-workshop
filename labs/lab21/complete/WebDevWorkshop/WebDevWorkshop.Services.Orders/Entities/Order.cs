namespace WebDevWorkshop.Services.Orders.Entities;

public class Order
{
    private int id;
    private List<OrderItem> _items = new();
    private List<Address> _addresses = new();

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private Order() { } // For EF Core
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private Order(string orderId, DeliveryAddress invoiceAddress, BillingAddress billingAddress, DateTimeOffset orderDate)
    {
        OrderId = orderId;
        _addresses.Add(invoiceAddress);
        _addresses.Add(billingAddress);
        OrderDate = orderDate;
    }

    public static Order Create(DeliveryAddress deliveryAddress, BillingAddress billingAddress)
        => new Order(GenerateOrderId(), deliveryAddress, billingAddress, DateTimeOffset.Now);

    public OrderItem AddItem(string name, int quantity, decimal price)
    {
        var item = OrderItem.Create(_items.Count + 1, name, quantity, price);
        _items.Add(item);
        Total += item.Price * item.Quantity;
        return item;
    }

    private static string GenerateOrderId()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
    }

    public string OrderId { get; private set; }
    public DateTimeOffset OrderDate { get; private set; }
    public decimal Total { get; private set; }
    public DeliveryAddress DeliveryAddress => _addresses.OfType<DeliveryAddress>().First();
    public BillingAddress BillingAddress => _addresses.OfType<BillingAddress>().First();
    public OrderItem[] Items => _items.ToArray();
}

public class OrderItem
{
    private int id;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private OrderItem() { } // For EF Core
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private OrderItem(int id, string name, int quantity, decimal price)
    {
        this.id = id;
        Name = name;
        Quantity = quantity;
        Price = price;
    }

    internal static OrderItem Create(int id, string name, int quantity, decimal price) => new OrderItem(id, name, quantity, price);

    public string Name { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public abstract class Address
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    protected Address() { } // For EF Core
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    protected Address(string name, string street1, string? street2,
                    string postalCode, string city, string country)
    {
        Name = name;
        Street1 = street1;
        Street2 = street2;
        PostalCode = postalCode;
        City = city;
        Country = country;
    }

    public string Name { get; private set; }
    public string Street1 { get; private set; }
    public string? Street2 { get; private set; }
    public string PostalCode { get; private set; }
    public string City { get; private set; }
    public string Country { get; private set; }
}

public class BillingAddress : Address
{
    private BillingAddress() { } // For EF Core
    private BillingAddress(string name, string street1, string? street2,
                        string postalCode, string city, string country)
        : base(name, street1, street2, postalCode, city, country)
    {
    }

    public static BillingAddress Create(string name, string street1, string? street2,
                        string postalCode, string city, string country)
        => new BillingAddress(name, street1, street2, postalCode, city, country);
}

public class DeliveryAddress : Address
{
    private DeliveryAddress() { } // For EF Core
    private DeliveryAddress(string name, string street1, string? street2,
                        string postalCode, string city, string country)
        : base(name, street1, street2, postalCode, city, country)
    {
    }

    public static DeliveryAddress Create(string name, string street1, string? street2,
                        string postalCode, string city, string country)
        => new DeliveryAddress(name, street1, street2, postalCode, city, country);
}
