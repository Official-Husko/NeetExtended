using System;
using System.Collections.Generic;
using Coop.Net;
using HarmonyLib;
using LiteNetLib.Utils;

namespace Coop.World.Economy;

/// <summary>
/// The stock market runs on the host. Its price ticks (every 0.9 s, all stocks) go to clients, who
/// put them into their own copy so the graphs move the same; a joining player gets the whole price
/// history. Buying and selling on a client is an order the host carries out: the money comes back as
/// the host's total, the shares as the new holding.
///
/// WorldStocks: mode, then
/// - Tick: count, per stock: ticker, price, profile;
/// - Full: count, per stock: ticker, profile, owned, price count, prices;
/// - Owned: ticker, owned.
/// WorldStockOrder (client to host): buy or sell, ticker, quantity.
/// </summary>
internal static class StockSync
{
    private const byte Tick = 0;
    private const byte Full = 1;
    private const byte Owned = 2;

    private static readonly Action<Stock, float> AddPrice =
        AccessTools.MethodDelegate<Action<Stock, float>>(AccessTools.Method(typeof(Stock), "AddPrice"));

    private static readonly List<Stock> Ticked = new List<Stock>(16);

    public static void Init()
    {
        Messages.On(MessageIds.WorldStocks, OnStocks);
        Messages.On(MessageIds.WorldStockOrder, OnOrder);
    }

    public static void Clear() => Ticked.Clear();

    /// <summary>Host, after a stock moved.</summary>
    public static void OnTicked(Stock stock)
    {
        if (WorldSide.Running && WorldSide.IsHost)
            Ticked.Add(stock);
    }

    public static void Flush()
    {
        if (Ticked.Count == 0)
            return;
        var w = Outbox.Begin(MessageIds.WorldStocks);
        w.Put(Tick);
        w.Put((byte)Ticked.Count);
        foreach (var stock in Ticked)
        {
            w.Put(stock.ticker);
            w.Put(stock.currentPrice);
            w.Put((byte)stock.currentStockProfile);
        }
        Outbox.ToPlayers(w, Channel.Events);
        Ticked.Clear();
    }

    /// <summary>Host: the whole market for one player (joining, resyncing).</summary>
    public static void SendAllTo(byte slot)
    {
        var market = StockMarket._;
        if (market?.stocks == null)
            return;
        var w = Outbox.Begin(MessageIds.WorldStocks);
        w.Put(Full);
        w.Put((byte)market.stocks.Count);
        foreach (var stock in market.stocks.Values)
        {
            w.Put(stock.ticker);
            w.Put((byte)stock.currentStockProfile);
            w.Put(stock.quantityOwned);
            w.Put((ushort)stock.prices.Count);
            foreach (float price in stock.prices)
                w.Put(price);
        }
        Outbox.ToSlot(slot, w, Channel.Events);
    }

    /// <summary>
    /// Client, instead of buying or selling: the order goes to the host. The answer the shop screen
    /// gets (and its confirm or cancel sound) is what the host will most likely do.
    /// </summary>
    public static bool Order(bool buy, string ticker, int qty, out float result)
    {
        result = 0f;
        var market = StockMarket._;
        if (!WorldSide.Running || !WorldSide.IsClient || market == null || !market.stocks.TryGetValue(ticker, out var stock))
            return true;
        float value = stock.currentPrice * qty;
        bool possible = buy ? HouseManager._.mainComputer.Money >= value : stock.quantityOwned >= qty;
        if (possible)
        {
            var w = Outbox.Begin(MessageIds.WorldStockOrder);
            w.Put(buy);
            w.Put(ticker);
            w.Put(qty);
            Outbox.ToHost(w, Channel.Events);
            result = value;
        }
        return false;
    }

    /// <summary>Host, after a real trade: everyone's holding of that stock.</summary>
    public static void Traded(string ticker)
    {
        var market = StockMarket._;
        if (!WorldSide.Running || !WorldSide.IsHost || market == null || !market.stocks.TryGetValue(ticker, out var stock))
            return;
        var w = Outbox.Begin(MessageIds.WorldStocks);
        w.Put(Owned);
        w.Put(ticker);
        w.Put(stock.quantityOwned);
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void OnOrder(in Sender from, NetDataReader reader)
    {
        bool buy = reader.GetBool();
        string ticker = reader.GetString();
        int qty = reader.GetInt();
        var market = StockMarket._;
        if (!WorldSide.IsHost || !WorldSide.Running || market == null || !market.stocks.ContainsKey(ticker) || qty <= 0)
            return;
        // The host's own checks, money and holding; Traded sends the result.
        if (buy)
            market.BuyStock(ticker, qty);
        else
            market.SellStock(ticker, qty);
    }

    private static void OnStocks(in Sender from, NetDataReader reader)
    {
        var market = StockMarket._;
        if (WorldSide.IsHost || !WorldSide.Running || market?.stocks == null)
            return;
        switch (reader.GetByte())
        {
            case Tick:
                ApplyTicks(market, reader);
                break;
            case Full:
                ApplyFull(market, reader);
                break;
            case Owned:
                ApplyOwned(market, reader.GetString(), reader.GetInt());
                break;
        }
    }

    private static void ApplyTicks(StockMarket market, NetDataReader reader)
    {
        int count = reader.GetByte();
        for (int i = 0; i < count; i++)
        {
            string ticker = reader.GetString();
            float price = reader.GetFloat();
            var profile = (Stock.StockProfile)reader.GetByte();
            if (!market.stocks.TryGetValue(ticker, out var stock) || stock.prices.Count == 0)
                continue;
            // The same as the stock's own ShiftPriceForward, with the host's price.
            stock.prices.RemoveAt(0);
            AddPrice(stock, price);
            stock.currentStockProfile = profile;
            stock.OnPriceChanged?.Invoke(price);
        }
    }

    private static void ApplyFull(StockMarket market, NetDataReader reader)
    {
        int count = reader.GetByte();
        for (int i = 0; i < count; i++)
        {
            string ticker = reader.GetString();
            var profile = (Stock.StockProfile)reader.GetByte();
            int owned = reader.GetInt();
            int prices = reader.GetUShort();
            market.stocks.TryGetValue(ticker, out var stock);
            if (stock != null)
                stock.prices.Clear();
            for (int p = 0; p < prices; p++)
            {
                float price = reader.GetFloat();
                if (stock != null)
                    AddPrice(stock, price);
            }
            if (stock == null || stock.prices.Count == 0)
                continue;
            stock.currentStockProfile = profile;
            stock.OnPriceChanged?.Invoke(stock.currentPrice);
            ApplyOwned(market, ticker, owned);
        }
    }

    private static void ApplyOwned(StockMarket market, string ticker, int owned)
    {
        if (!market.stocks.TryGetValue(ticker, out var stock) || stock.quantityOwned == owned)
            return;
        bool bought = owned > stock.quantityOwned;
        stock.quantityOwned = owned;
        if (bought)
            stock.OnBought?.Invoke(owned);
        else
            stock.OnSold?.Invoke(owned);
    }
}
