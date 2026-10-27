using System.Collections.Generic;
using Coop.Net;
using Coop.World.Objects;
using Coop.World.Spawning;
using Coop.World.State;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Flames;

/// <summary>
/// Every flame on an item or in the house (candles, lighters, cigarettes, an item that caught fire) is
/// lit or out, at the same heat, in every game. The game that simulates the flame sends it when it
/// lights; a flame put out is sent from whichever game put it out. The hooks (FlamePatches) note a
/// change the moment it happens and the end of the frame sends it, batched. An item that catches fire
/// gets a flame added to it, and everyone else's copy gets one too; it goes when the fire does.
///
/// WorldFlame (any game, passed on by the host): count, then per flame: ref, index among the item's
/// flames, state (bit 0 lit, bit 1 added by a fire, bits 2 and up heat).
/// </summary>
internal static class FlameSync
{
    private static readonly List<FlameInfo> Pending = new List<FlameInfo>(16);
    private static readonly List<FlameInfo> Burning = new List<FlameInfo>(16);
    private static readonly List<FlameInfo> Sending = new List<FlameInfo>(16);

    /// <summary>True while a received state is applied: the hooks stay quiet and nothing is held back.</summary>
    public static bool Applying { get; private set; }

    public static void Init() => Messages.On(MessageIds.WorldFlame, OnFlame);

    public static void Clear()
    {
        Pending.Clear();
        Burning.Clear();
        Sending.Clear();
        FlameBook.Clear();
    }

    /// <summary>From the hooks: this flame just changed (or may have).</summary>
    public static void Changed(Flame flame)
    {
        if (!WorldSide.Running || Applying)
            return;
        var info = FlameBook.Info(flame);
        if (info == null)
            return;
        Note(info);
    }

    /// <summary>From the hooks: an item caught fire.</summary>
    public static void Caught(Flame flame)
    {
        if (!WorldSide.Running || Applying || SpawnContext.Silent)
            return;
        var info = FlameBook.Added(flame);
        if (info == null)
            return;
        Burning.Add(info);
        Note(info);
    }

    /// <summary>End of the frame: sends what changed.</summary>
    public static void LateTick()
    {
        // Flames the game removed by itself (the fire burned out) are noticed here.
        for (int i = Burning.Count - 1; i >= 0; i--)
        {
            if (Burning[i].Flame == null)
            {
                Note(Burning[i]);
                Burning.RemoveAt(i);
            }
        }
        if (Pending.Count == 0)
            return;
        Sending.Clear();
        foreach (var info in Pending)
        {
            info.Pending = false;
            if (Decide(info))
                Sending.Add(info);
        }
        Pending.Clear();
        if (Sending.Count > 0)
            Send();
    }

    private static void Note(FlameInfo info)
    {
        if (info.Pending)
            return;
        info.Pending = true;
        Pending.Add(info);
    }

    // Whether this flame's new state goes out; remembers it either way.
    private static bool Decide(FlameInfo info)
    {
        byte now = info.Flame != null ? FlameBook.Pack(info.Flame, info.Runtime) : (byte)0;
        if (now == info.Last)
            return false;
        bool wentOut = FlameBook.IsLit(info.Last) && !FlameBook.IsLit(now);
        info.Last = now;
        if (info.Flame == null)
            FlameBook.Forget(info);
        return wentOut || FlameRules.Simulates(info);
    }

    private static void Send()
    {
        var w = Outbox.Begin(MessageIds.WorldFlame);
        w.Put((ushort)Sending.Count);
        foreach (var info in Sending)
        {
            info.Ref.Write(w);
            w.Put(info.Index);
            w.Put(info.Last);
        }
        Outbox.ToPlayers(w, Channel.Events);
        Sending.Clear();
    }

    private static void OnFlame(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        if (WorldSide.IsHost)
            Relay.Forward(MessageIds.WorldFlame, reader.RawData, start, length, Channel.Events, from.Slot);
        if (!WorldSide.Running)
            return;
        int count = reader.GetUShort();
        for (int i = 0; i < count; i++)
        {
            var r = NetRef.Read(reader);
            byte index = reader.GetByte();
            byte state = reader.GetByte();
            Apply(r, index, state);
        }
    }

    private static void Apply(NetRef r, byte index, byte state)
    {
        var info = FlameBook.Find(r, index);
        Applying = true;
        try
        {
            if (info == null && FlameBook.IsRuntime(state) && FlameBook.IsLit(state))
                info = Catch(r);
            if (info == null || info.Flame == null)
                return;
            var flame = info.Flame;
            bool lit = FlameBook.IsLit(state);
            if (lit && !flame.lit)
                flame.Ignite(force: true);
            else if (!lit && flame.lit)
                flame.Extinguish();
            if (lit && flame.lit)
                flame.HeatLevel = FlameBook.Heat(state);
            info.Last = flame != null ? FlameBook.Pack(flame, info.Runtime) : (byte)0;
        }
        catch (System.Exception e)
        {
            CoopMod.Log.Debug($"World: couldn't apply a flame of {r}: {e.Message}");
        }
        finally
        {
            Applying = false;
        }
    }

    // The same fire the game starts on an item that touches a flame.
    private static FlameInfo Catch(NetRef r)
    {
        var o = NetObjects.Find(r);
        if (o == null || !o.Alive || o.Usable == null || o.Body == null || o.Go.GetComponentInChildren<Flame>(true) != null)
            return null;
        var flame = Flame.AddBurnCollider(o.Go);
        var info = FlameBook.Added(flame);
        if (info != null)
            Burning.Add(info);
        return info;
    }
}
