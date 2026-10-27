using System.Text;
using Coop.World.Objects;
using Coop.World.Simulation;
using Coop.World.Streaming;
using UnityEngine;

namespace Coop.World;

/// <summary>
/// Checks for the dev bridge, e.g. <c>neet call type.call type=Coop.World.WorldApi method=Summary --instance 2</c>.
/// Compare <see cref="Hash"/> between the games to see which kind of thing drifted.
/// </summary>
public static class WorldApi
{
    public static string Summary()
    {
        if (!WorldSide.Running)
            return "World sync isn't running.";
        int items = 0, npcs = 0, doors = 0, paths = 0, replicas = 0, ownedHere = 0, held = 0;
        foreach (var o in NetObjects.All)
        {
            switch (o.Ref.Kind)
            {
                case NetKind.Item: items++; break;
                case NetKind.Npc: npcs++; break;
                case NetKind.Door: doors++; break;
                default: paths++; break;
            }
            if (o.IsReplica)
                replicas++;
            if (o.OwnedHere && o.Brain == null && WorldSide.LocalSlot != WorldSide.HostSlot)
                ownedHere++;
            if (o.IsHeld)
                held++;
        }
        return $"{(WorldSide.IsHost ? "host" : "client")} slot {WorldSide.LocalSlot}: {items} items, {npcs} NPCs, {doors} doors, {paths} scene objects; " +
            $"{replicas} replicas ({ReplicaDriver.Count} moving), {ownedHere} owned by this client, {held} held; id counter {SaveLoadHandler.idSequence}; " +
            $"money {(HouseManager._?.mainComputer != null ? HouseManager._.mainComputer.Money : 0f)}";
    }

    /// <summary>What isn't simulated by the host, and who holds what.</summary>
    public static string Owned()
    {
        var text = new StringBuilder();
        foreach (var o in NetObjects.All)
        {
            if (o.Owner == WorldSide.HostSlot && o.Holder == WorldSide.None)
                continue;
            text.Append(o).Append(": owner ").Append(o.Owner);
            if (o.IsHeld)
                text.Append(", held by ").Append(o.Holder);
            text.Append(o.IsReplica ? ", replica" : ", simulated here").Append('\n');
        }
        return text.Length == 0 ? "Everything is the host's." : text.ToString();
    }

    /// <summary>Money, the first stocks (price and holding), tax bills and the card game, to compare between games.</summary>
    public static string Economy()
    {
        var text = new StringBuilder();
        var computer = HouseManager._?.mainComputer;
        text.Append("money ").Append(computer != null ? computer.Money : 0f);
        var market = StockMarket._;
        if (market?.stocks != null)
        {
            text.Append("\nstocks");
            int shown = 0;
            foreach (var stock in market.stocks.Values)
            {
                if (shown++ == 5)
                    break;
                text.Append(' ').Append(stock.ticker).Append(' ').Append(stock.currentPrice.ToString("0.00")).Append('x').Append(stock.quantityOwned);
            }
        }
        var taxes = HouseManager._ != null ? HouseManager._.GetComponent<TaxHandler>() : null;
        if (taxes != null)
        {
            text.Append("\ntax bills");
            foreach (var bill in taxes.taxBills)
                text.Append(' ').Append(bill.amount.ToString("0")).Append('/').Append(bill.stage);
        }
        var cards = computer != null ? computer.animeCardGenerator : null;
        if (cards != null)
            text.Append("\ncrystals ").Append(Mathf.Round(cards.Crystals)).Append(", unlocked ").Append(cards.maxCardUnlocked);
        return text.ToString();
    }

    /// <summary>Every shared component's current state bytes, one per line (diff between games).</summary>
    public static string States()
    {
        var text = new StringBuilder();
        foreach (var t in Coop.World.State.StateTrackers.All)
        {
            Coop.World.State.StateSync.Capture(t);
            text.Append(t.Type.GetType().Name).Append(' ').Append(t.Ref).Append(' ');
            foreach (byte b in t.Last)
                text.Append(b.ToString("x2"));
            text.Append('\n');
        }
        return text.ToString();
    }

    /// <summary>World's own frame cost since the last call (then starts counting again).</summary>
    public static string Timing() => WorldTiming.Report();

    /// <summary>Client: anything that should be switched off but runs.</summary>
    public static string Leaks() => WorldSide.IsClient ? ClientSwitches.Leaks() : "Not a client.";

    /// <summary>
    /// Per kind: how many, a hash of the ids, and a hash of ids with positions rounded to 10 cm (things
    /// at rest should match between games; moving ones won't).
    /// </summary>
    public static string Hash()
    {
        var text = new StringBuilder();
        foreach (NetKind kind in new[] { NetKind.Item, NetKind.Npc, NetKind.Door, NetKind.Path })
        {
            int count = 0;
            uint ids = 0, places = 0;
            foreach (var o in NetObjects.All)
            {
                if (o.Ref.Kind != kind || !o.Alive)
                    continue;
                count++;
                uint id = Mix((uint)o.Ref.Id);
                ids += id;
                var p = o.Tf.position;
                places += Mix(id ^ Mix((uint)Mathf.RoundToInt(p.x * 10f)) ^ Mix((uint)Mathf.RoundToInt(p.y * 10f) * 31u) ^ Mix((uint)Mathf.RoundToInt(p.z * 10f) * 131u));
            }
            text.Append(kind).Append(": ").Append(count).Append(" ids ").Append(ids.ToString("x8")).Append(" places ").Append(places.ToString("x8")).Append('\n');
        }
        return text.ToString();
    }

    /// <summary>The ones whose rounded place differs is what <see cref="Hash"/> can't say; this lists one kind's places.</summary>
    public static string Places(string kind)
    {
        var text = new StringBuilder();
        foreach (var o in NetObjects.All)
        {
            if (!string.Equals(o.Ref.Kind.ToString(), kind, System.StringComparison.OrdinalIgnoreCase) || !o.Alive)
                continue;
            var p = o.Tf.position;
            text.Append(o.Ref.Id).Append(' ').Append(Mathf.RoundToInt(p.x * 10f)).Append(',').Append(Mathf.RoundToInt(p.y * 10f)).Append(',').Append(Mathf.RoundToInt(p.z * 10f))
                .Append(' ').Append(o.PrefabName.Length > 0 ? o.PrefabName : o.Go.name).Append('\n');
        }
        return text.ToString();
    }

    /// <summary>Every body whose object name contains <paramref name="name"/>: height, kinematic, and what it is shared as.</summary>
    public static string Bodies(string name)
    {
        var text = new StringBuilder();
        foreach (var body in Object.FindObjectsOfType<Rigidbody>())
        {
            if (body.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            var o = NetObjects.Find(body.gameObject);
            text.Append(body.GetInstanceID()).Append(' ').Append(body.name).Append(" y ").Append(Mathf.RoundToInt(body.position.y))
                .Append(body.isKinematic ? " kinematic" : " dynamic");
            if (o == null)
                text.Append(", not shared");
            else
                text.Append(", ").Append(o.Ref).Append(o.IsReplica ? " replica" : " simulated here").Append(" owner ").Append(o.Owner)
                    .Append(o.Go == body.gameObject ? "" : " (other object)");
            text.Append('\n');
        }
        return text.ToString();
    }

    private static uint Mix(uint x)
    {
        unchecked
        {
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return x;
        }
    }
}
