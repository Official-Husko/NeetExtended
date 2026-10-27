namespace Coop.World.Economy;

/// <summary>
/// The computer as one part of world sync: the wallet, the stock market, tax bills, the card game,
/// the inbox, store orders and apps. All of it runs on the host; clients see its state and send their
/// actions as requests.
/// </summary>
internal static class EconomySync
{
    public static void Init()
    {
        MoneySync.Init();
        StockSync.Init();
        TaxSync.Init();
        CardSync.Init();
        InboxSync.Init();
        Purchases.Init();
        AppSync.Init();
        CustomBoxes.Init();
    }

    /// <summary>A new house or session: forget what was sent.</summary>
    public static void Clear()
    {
        MoneySync.Reset();
        StockSync.Clear();
        TaxSync.Clear();
        CardSync.Clear();
        Purchases.Clear();
        AppSync.Clear();
        CustomBoxes.Clear();
    }

    /// <summary>Host: everything for a player who joined or resynced.</summary>
    public static void SendTo(byte slot)
    {
        MoneySync.SendTo(slot);
        StockSync.SendAllTo(slot);
        TaxSync.SendAgain();
        CardSync.SendAgain();
        InboxSync.SendAllTo(slot);
        AppSync.SendTo(slot);
    }

    /// <summary>Every frame: money written behind the setter's back.</summary>
    public static void Update() => MoneySync.Check();

    /// <summary>Late every frame, once the frame's spawns went out: what has to follow them.</summary>
    public static void AfterSpawns() => CustomBoxes.Flush();

    /// <summary>Host, late every frame: what changed goes out.</summary>
    public static void HostLateUpdate()
    {
        StockSync.Flush();
        TaxSync.Tick();
        CardSync.Tick();
    }
}
