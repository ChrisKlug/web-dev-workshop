namespace WebDevWorkshop.Web.ShoppingCart;

public interface IShoppingCart : IGrainWithStringKey
{
    Task AddItem(ShoppingCartItem item);
    Task<ShoppingCartItem[]> GetItems();
}

[GenerateSerializer]
public class ShoppingCartItem
{
    [Id(1)] public int ProductId { get; set; }
    [Id(2)] public string ProductName { get; set; }
    [Id(3)] public decimal Price { get; set; }
    [Id(4)] public int Count { get; set; }
}

public class ShoppingCartGrain : Grain, IShoppingCart
{
    private readonly IPersistentState<ShoppingCartState> _state;
    public ShoppingCartGrain([PersistentState("ShoppingCartState")] IPersistentState<ShoppingCartState> state)
    {
        _state = state;
    }

    public Task AddItem(ShoppingCartItem item)
    {
        var existingItem = _state.State.Items.FirstOrDefault(x => x.ProductId == item.ProductId);
        if (existingItem is null)
        {
            _state.State.Items.Add(item);
        }
        else
        {
            existingItem.Count += item.Count;
        }
        return _state.WriteStateAsync();
    }

    public Task<ShoppingCartItem[]> GetItems()
        => Task.FromResult(_state.State.Items.ToArray());
}

public class ShoppingCartState
{
    public List<ShoppingCartItem> Items { get; set; } = new();
}
