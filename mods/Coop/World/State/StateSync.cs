using System;
using System.Collections.Generic;
using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State;

/// <summary>
/// Sends and applies state. A change goes out from whichever game made it: the hint patches mark a
/// component the moment a player uses it, and the game that watches a component (its item's owner,
/// the host for scene things) also sweeps through them twice a second for changes the game makes on
/// its own. The host applies what clients send and echoes the result to everyone, so two players
/// flipping the same switch at once both end on the host's order.
///
/// WorldState: count, then per change: ref, type index, length, bytes.
/// </summary>
internal static class StateSync
{
    private const float SweepSeconds = 0.5f;

    private static readonly NetDataWriter Scratch = new NetDataWriter(true, 64);
    private static readonly NetDataReader Slice = new NetDataReader();
    private static readonly byte[] Incoming = new byte[256];
    private static readonly List<StateTracker> Changed = new List<StateTracker>();
    private static readonly List<StateTracker> Marked = new List<StateTracker>();
    private static readonly HashSet<Type> Failing = new HashSet<Type>();
    private static int _cursor;
    private static float _carry;

    /// <summary>True while a received state is applied: the hints stay quiet.</summary>
    public static bool Applying { get; private set; }

    public static void Init() => Messages.On(MessageIds.WorldState, OnState);

    public static void Clear()
    {
        Changed.Clear();
        Marked.Clear();
        _cursor = 0;
        _carry = 0f;
    }

    /// <summary>From the hint patches: someone here just used it.</summary>
    public static void MarkDirty(Component component)
    {
        if (!WorldSide.Running || Applying)
            return;
        var t = StateTrackers.Find(component);
        if (t == null || t.Dirty)
            return;
        t.Dirty = true;
        Marked.Add(t);
    }

    /// <summary>Takes the component's current bytes as known (no change to send).</summary>
    public static void Capture(StateTracker t)
    {
        if (Write(t))
            t.Remember(Scratch.Data, 0, Scratch.Length);
    }

    public static void LateTick()
    {
        for (int i = 0; i < Marked.Count; i++)
        {
            if (Marked[i].Dirty)
                Check(Marked[i]);
        }
        Marked.Clear();
        var all = StateTrackers.All;
        int count = all.Count;
        if (count > 0)
        {
            // Spread the sweep over SweepSeconds, whatever the frame rate.
            _carry += count * Time.unscaledDeltaTime / SweepSeconds;
            int n = Mathf.Min(count, (int)_carry);
            _carry -= n;
            for (int k = 0; k < n; k++)
            {
                var t = all[_cursor++ % count];
                if (t.WatchedHere)
                    Check(t);
            }
        }
        Flush();
    }

    /// <summary>Host: every shared component's current state, for a manifest.</summary>
    public static void WriteAll(NetDataWriter w)
    {
        var all = StateTrackers.All;
        w.Put(all.Count);
        foreach (var t in all)
        {
            Capture(t);
            Put(w, t);
        }
    }

    /// <summary>Client: the states in a manifest.</summary>
    public static int ReadAll(NetDataReader reader)
    {
        int count = reader.GetInt();
        int applied = 0;
        for (int i = 0; i < count; i++)
        {
            if (!ReadOne(reader, out var t, out int length) || !Apply(t, Incoming, length))
                continue;
            if (++applied <= 3)
                CoopMod.Log.Info($"World: the host's {t.Type.GetType().Name} for {t.Component.name} ({t.Ref}) differed here.");
        }
        return applied;
    }

    private static void Check(StateTracker t)
    {
        t.Dirty = false;
        if (!Write(t) || t.Same(Scratch.Data, Scratch.Length))
            return;
        t.Remember(Scratch.Data, 0, Scratch.Length);
        if (!Changed.Contains(t))
            Changed.Add(t);
    }

    private static bool Write(StateTracker t)
    {
        Scratch.Reset();
        if (t.Component == null)
            return false;
        try
        {
            t.Type.Write(t.Component, Scratch);
            return true;
        }
        catch (Exception e)
        {
            if (Failing.Add(t.Type.GetType()))
                CoopMod.Log.Warn($"World: reading {t.Type.GetType().Name} failed: {e.Message}");
            return false;
        }
    }

    private static void Flush()
    {
        if (Changed.Count == 0)
            return;
        var w = Outbox.Begin(MessageIds.WorldState);
        w.Put((ushort)Changed.Count);
        foreach (var t in Changed)
            Put(w, t);
        Outbox.ToPlayers(w, Channel.Events);
        Changed.Clear();
    }

    private static void Put(NetDataWriter w, StateTracker t)
    {
        t.Ref.Write(w);
        w.Put(t.Type.Index);
        w.Put((byte)t.Last.Length);
        w.Put(t.Last);
    }

    private static void OnState(in Sender from, NetDataReader reader)
    {
        if (!WorldSide.Running)
            return;
        int count = reader.GetUShort();
        for (int i = 0; i < count; i++)
        {
            if (!ReadOne(reader, out var t, out int length))
                continue;
            Apply(t, Incoming, length);
            // The host's word goes back to everyone, the sender included.
            if (WorldSide.IsHost && !Changed.Contains(t))
                Changed.Add(t);
        }
    }

    private static bool ReadOne(NetDataReader reader, out StateTracker t, out int length)
    {
        var r = NetRef.Read(reader);
        byte type = reader.GetByte();
        length = reader.GetByte();
        reader.GetBytes(Incoming, length);
        t = StateTrackers.Find(r, type);
        return t != null && t.Component != null;
    }

    private static bool Apply(StateTracker t, byte[] data, int length)
    {
        if (t.Same(data, length))
            return false;
        Slice.SetSource(data, 0, length);
        Applying = true;
        try
        {
            t.Type.Apply(t.Component, Slice);
        }
        catch (Exception e)
        {
            CoopMod.Log.Warn($"World: applying {t.Type.GetType().Name} to {t.Component.name} failed: {e.Message}");
        }
        finally
        {
            Applying = false;
        }
        // What's really there now (an apply can fall short, e.g. a fuse that hasn't arrived yet).
        Capture(t);
        return true;
    }
}
