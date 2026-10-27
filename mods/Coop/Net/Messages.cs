using System;
using LiteNetLib.Utils;

namespace Coop.Net;

/// <summary>Who a message came from: the transport's peer and the player slot behind it.</summary>
public readonly struct Sender
{
    public readonly int Peer;
    public readonly byte Slot;

    public Sender(int peer, byte slot)
    {
        Peer = peer;
        Slot = slot;
    }
}

public delegate void MessageHandler(in Sender from, NetDataReader reader);

/// <summary>
/// Routes incoming messages to handlers by id. Every message starts with its ushort id
/// (<see cref="Outbox.Begin"/> writes it). Handlers run on the main thread and must not keep the reader.
/// </summary>
public static class Messages
{
    private static readonly MessageHandler[] Handlers = new MessageHandler[256];

    /// <summary>One handler per id; registering again replaces it.</summary>
    public static void On(ushort id, MessageHandler handler)
    {
        if (id >= Handlers.Length)
            throw new ArgumentOutOfRangeException(nameof(id), "Message ids go up to 255.");
        Handlers[id] = handler;
    }

    internal static void Dispatch(in Sender from, NetDataReader reader)
    {
        if (reader.AvailableBytes < 2)
            return;
        ushort id = reader.GetUShort();
        var handler = id < Handlers.Length ? Handlers[id] : null;
        if (handler == null)
        {
            CoopMod.Log?.Warn($"Got message {id} from slot {from.Slot}, which nothing handles.");
            return;
        }
        try
        {
            handler(in from, reader);
        }
        catch (Exception e)
        {
            CoopMod.Log?.Error($"Message {id} from slot {from.Slot} failed: {e}");
        }
    }

    internal static void Clear() => Array.Clear(Handlers, 0, Handlers.Length);
}
