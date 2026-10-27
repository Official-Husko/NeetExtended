namespace NeetModKit.Events;

/// <summary>An order from one of the computer's stores.</summary>
public sealed class StoreOrder
{
    public StoreOrder(string item, string store)
    {
        Item = item;
        Store = store;
    }

    /// <summary>Prefab name of the ordered item.</summary>
    public string Item { get; }

    /// <summary>The store: Normal, BlackMarket or OccultMarket (the game's PurchaseableAt), or null if unknown.</summary>
    public string Store { get; }
}
