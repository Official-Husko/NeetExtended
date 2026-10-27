using System;
using System.Collections.Generic;
using Coop.Net;
using LiteNetLib.Utils;

namespace Coop.World.Painting;

/// <summary>
/// Host: a joining player's house is the snapshot taken when they arrived, and they only get world
/// messages once it has loaded. Paint made in between (by anyone) is kept here and sent to them when
/// they're ready, in order, before any newer paint. Up to 1 MB per joiner; past that the rest is lost
/// for them (logged).
/// </summary>
internal static class PaintCatchUp
{
    private const int MaxBytes = 1 << 20;

    private sealed class Backlog
    {
        public byte Slot;
        public int Bytes;
        public bool Full;
        public readonly List<(ushort Id, byte[] Payload)> Messages = new List<(ushort, byte[])>();
    }

    private static readonly List<Backlog> Backlogs = new List<Backlog>();

    public static void Start(byte slot)
    {
        Forget(slot);
        Backlogs.Add(new Backlog { Slot = slot });
    }

    /// <summary>A paint message just sent to the players in the house (written from the message id on).</summary>
    public static void Record(NetDataWriter message)
    {
        if (Backlogs.Count == 0 || message.Length < 2)
            return;
        ushort id = BitConverter.ToUInt16(message.Data, 0);
        byte[] payload = null;
        foreach (var backlog in Backlogs)
        {
            if (backlog.Full)
                continue;
            if (backlog.Bytes + message.Length > MaxBytes)
            {
                backlog.Full = true;
                CoopMod.Log.Warn($"Paint: more than {MaxBytes / 1024} KB of paint while slot {backlog.Slot} was loading; the rest won't show for them.");
                continue;
            }
            if (payload == null)
            {
                payload = new byte[message.Length - 2];
                Buffer.BlockCopy(message.Data, 2, payload, 0, payload.Length);
            }
            backlog.Messages.Add((id, payload));
            backlog.Bytes += message.Length;
        }
    }

    /// <summary>The player has the house loaded: everything kept for them goes out now.</summary>
    public static void Deliver(byte slot)
    {
        int index = IndexOf(slot);
        if (index < 0)
            return;
        var backlog = Backlogs[index];
        Backlogs.RemoveAt(index);
        foreach (var (id, payload) in backlog.Messages)
        {
            var w = Outbox.Begin(id);
            w.Put(payload);
            Outbox.ToSlot(slot, w, Channel.State);
        }
        if (backlog.Messages.Count > 0)
            CoopMod.Log.Info($"Paint: sent slot {slot} the {backlog.Messages.Count} paint messages ({backlog.Bytes / 1024} KB) made while they loaded.");
    }

    public static void Forget(byte slot)
    {
        int index = IndexOf(slot);
        if (index >= 0)
            Backlogs.RemoveAt(index);
    }

    public static void Clear() => Backlogs.Clear();

    private static int IndexOf(byte slot)
    {
        for (int i = 0; i < Backlogs.Count; i++)
        {
            if (Backlogs[i].Slot == slot)
                return i;
        }
        return -1;
    }
}
