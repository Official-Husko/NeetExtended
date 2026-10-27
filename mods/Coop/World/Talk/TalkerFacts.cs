using System;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace Coop.World.Talk;

/// <summary>
/// What the framework's rules ask about a player (stats, held items), sent by a remote player's
/// game with every conversation request and phone button. The host keeps the latest per player, so
/// the <c>stat</c> and <c>holding</c> conditions in their conversation read their own body.
/// </summary>
internal static class TalkerFacts
{
    internal sealed class Facts
    {
        public readonly Dictionary<string, float> Stats = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Held = new List<string>(2);
    }

    private static readonly Dictionary<byte, Facts> BySlot = new Dictionary<byte, Facts>();

    public static void Clear() => BySlot.Clear();

    public static void Forget(byte slot) => BySlot.Remove(slot);

    /// <summary>Host: the latest facts from <paramref name="slot"/>, or null before their first request.</summary>
    public static Facts Of(byte slot) => BySlot.TryGetValue(slot, out var facts) ? facts : null;

    /// <summary>Client: this game's player's stats and held items.</summary>
    public static void Write(NetDataWriter w)
    {
        var player = WorldSide.LocalPlayer;
        var stats = player != null ? player.GetComponent<PlayerStats>() : null;
        if (stats == null || stats.statTypes == null)
            w.Put((byte)0);
        else
        {
            int count = Math.Min(stats.statTypes.Count, byte.MaxValue);
            w.Put((byte)count);
            for (int i = 0; i < count; i++)
            {
                var type = stats.statTypes[i];
                w.Put(type.Name);
                w.Put(stats.stats.TryGetValue(type, out var stat) ? stat.GetVal() : 0f);
            }
        }

        var hands = player != null ? player.GetComponent<Player_Abilities>() : null;
        var pickups = hands != null ? hands.pickups : null;
        int held = 0;
        for (int i = 0; pickups != null && i < pickups.Length && i < 2; i++)
        {
            if (pickups[i] != null)
                held++;
        }
        w.Put((byte)held);
        for (int i = 0; held > 0 && i < pickups.Length && i < 2; i++)
        {
            if (pickups[i] != null)
                w.Put(NeetModKit.Game.Spawning.PrefabName(pickups[i].name));
        }
    }

    /// <summary>Host: reads what <see cref="Write"/> sent and keeps it for <paramref name="slot"/>.</summary>
    public static void Read(NetDataReader r, byte slot)
    {
        if (!BySlot.TryGetValue(slot, out var facts))
            BySlot[slot] = facts = new Facts();
        facts.Stats.Clear();
        int count = r.GetByte();
        for (int i = 0; i < count; i++)
        {
            string name = r.GetString();
            facts.Stats[name] = r.GetFloat();
        }
        facts.Held.Clear();
        int held = r.GetByte();
        for (int i = 0; i < held; i++)
            facts.Held.Add(r.GetString());
    }
}
