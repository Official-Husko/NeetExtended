using System.Collections.Generic;
using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Breakables;

/// <summary>
/// Breaking things that aren't items. The host decides for the house's own breakables (window panes,
/// glass cases, walls): a client whose throw or body hit one asks, the host runs the game's own break
/// (count, sound, shards, the pane going away) and tells everyone how it ended. Anything an owner
/// breaks that is an item (a plate, a mug, the piggy bank) is already decided where it's simulated;
/// the others only get the sound and the particles that aren't items, through the same event.
///
/// WorldBreakRequest (client to host): breakable id, mode (0 hit, 1 force break), direction, extra force.
/// WorldBreak: flags (1 = broken, 2 = scene breakable), then for a scene breakable its id and hit count,
/// for an item its net ref. Items' events go from their owner via the host to the others.
/// WorldBreakSnapshot (host to a joining player): count, then per intact scene breakable its id and
/// hit count. Anything the player has that isn't listed is already broken on the host.
/// </summary>
internal static class BreakNet
{
    private const byte Broken = 1;
    private const byte Scene = 2;

    private static readonly HashSet<int> Intact = new HashSet<int>();
    private static readonly List<int> SnapshotIds = new List<int>();
    private static readonly List<int> SnapshotCounts = new List<int>();

    public static void Init()
    {
        Messages.On(MessageIds.WorldBreakRequest, OnRequest);
        Messages.On(MessageIds.WorldBreak, OnBreak);
        Messages.On(MessageIds.WorldBreakSnapshot, OnSnapshot);
    }

    /// <summary>Client: a hit on a house breakable, for the host to decide.</summary>
    public static void Request(int id, bool force, Vector3 direction, float extraForce)
    {
        var w = Outbox.Begin(MessageIds.WorldBreakRequest);
        w.Put(id);
        w.Put(force);
        w.Put(direction.x);
        w.Put(direction.y);
        w.Put(direction.z);
        w.Put(extraForce);
        Outbox.ToHost(w, Channel.Events);
    }

    /// <summary>Host: a house breakable was hit or broke here.</summary>
    public static void AnnounceScene(int id, BreakOnImpact b)
    {
        var w = Outbox.Begin(MessageIds.WorldBreak);
        w.Put((byte)(Scene | (b.broken ? Broken : 0)));
        w.Put(id);
        w.Put((byte)Mathf.Clamp(b.breakCollisionAmounts, 0, 255));
        Outbox.ToPlayers(w, Channel.Events);
    }

    /// <summary>The game that simulates an item: it broke here.</summary>
    public static void AnnounceItem(NetObject o)
    {
        var w = Outbox.Begin(MessageIds.WorldBreak);
        w.Put(Broken);
        o.Ref.Write(w);
        Outbox.ToPlayers(w, Channel.Events);
    }

    /// <summary>Host: tells a player who just loaded the house which breakables are still whole.</summary>
    public static void SendSnapshot(byte slot)
    {
        SnapshotIds.Clear();
        foreach (var pair in SceneBreakers.All)
        {
            if (pair.Value != null && !pair.Value.broken)
                SnapshotIds.Add(pair.Key);
        }
        var w = Outbox.Begin(MessageIds.WorldBreakSnapshot);
        w.Put((ushort)SnapshotIds.Count);
        foreach (int id in SnapshotIds)
        {
            w.Put(id);
            w.Put((byte)Mathf.Clamp(SceneBreakers.Find(id).breakCollisionAmounts, 0, 255));
        }
        Outbox.ToSlot(slot, w, Channel.Events);
    }

    private static void OnRequest(in Sender from, NetDataReader reader)
    {
        int id = reader.GetInt();
        bool force = reader.GetBool();
        var direction = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        float extra = reader.GetFloat();
        if (!WorldSide.Running || !WorldSide.IsHost)
            return;
        var b = SceneBreakers.Find(id);
        if (b == null || b.broken)
            return;
        if (force)
            b.ForceBreak();
        else
            b.Impact(direction, null, extra);
    }

    private static void OnBreak(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        byte flags = reader.GetByte();
        bool scene = (flags & Scene) != 0;
        bool broken = (flags & Broken) != 0;
        if (scene)
        {
            int id = reader.GetInt();
            byte count = reader.GetByte();
            if (!WorldSide.Running || WorldSide.IsHost)
                return; // The host is where scene breaks are decided.
            var b = SceneBreakers.Find(id);
            if (b == null || b.broken)
                return;
            if (broken)
                BreakApply.Break(b);
            else
                BreakApply.Hit(b, count);
            return;
        }
        var r = NetRef.Read(reader);
        if (WorldSide.IsHost)
            Relay.Forward(MessageIds.WorldBreak, reader.RawData, start, length, Channel.Events, from.Slot);
        if (!WorldSide.Running)
            return;
        var o = NetObjects.Find(r);
        var item = o != null && o.Alive ? o.Go.GetComponentInChildren<BreakOnImpact>(true) : null;
        if (item != null && !item.broken)
            BreakApply.Effects(item);
    }

    private static void OnSnapshot(in Sender from, NetDataReader reader)
    {
        int count = reader.GetUShort();
        if (!WorldSide.Running || WorldSide.IsHost)
            return;
        Intact.Clear();
        SnapshotIds.Clear();
        SnapshotCounts.Clear();
        for (int i = 0; i < count; i++)
        {
            int id = reader.GetInt();
            SnapshotIds.Add(id);
            SnapshotCounts.Add(reader.GetByte());
            Intact.Add(id);
        }
        int gone = 0;
        foreach (var pair in SceneBreakers.All)
        {
            var b = pair.Value;
            if (b == null || b.broken || Intact.Contains(pair.Key))
                continue;
            BreakApply.Quiet(b, b.breakCollisionAmounts, true);
            gone++;
        }
        for (int i = 0; i < count; i++)
        {
            var b = SceneBreakers.Find(SnapshotIds[i]);
            if (b != null && !b.broken)
                b.breakCollisionAmounts = SnapshotCounts[i];
        }
        if (gone > 0)
            CoopMod.Log.Info($"World: {gone} breakables were already broken on the host.");
    }
}
