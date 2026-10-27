using System.Collections.Generic;
using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Economy;

/// <summary>
/// Boxes from the custom shops carry what they do to their item when opened (a picture or a song
/// from a file). The game keeps that on the box only, so once such a box is shared its
/// <see cref="BoxCustomization"/> follows it to every game, and whoever opens it unpacks the custom item.
///
/// WorldBoxExtra: box ref, kind, file path. Clients send theirs to the host, which passes them on.
/// </summary>
internal static class CustomBoxes
{
    // A box the game never shares (it would have to lose its item name) stops being waited for.
    private const int WaitFrames = 30;

    private struct Waiting
    {
        public Cardboard_Box Box;
        public int Since;
    }

    private static readonly List<Waiting> Pending = new List<Waiting>();

    public static void Init() => Messages.On(MessageIds.WorldBoxExtra, OnBox);

    public static void Clear() => Pending.Clear();

    /// <summary>After a box's Start: one with an unboxing step waits until it's shared.</summary>
    public static void Started(Cardboard_Box box)
    {
        if (WorldSide.Running && box.OnUnboxingApplyToContents != null)
            Pending.Add(new Waiting { Box = box, Since = Time.frameCount });
    }

    /// <summary>Late every frame, after the frame's spawns went out.</summary>
    public static void Flush()
    {
        for (int i = Pending.Count - 1; i >= 0; i--)
        {
            var waiting = Pending[i];
            var box = waiting.Box;
            var o = box != null ? NetObjects.Find(box.gameObject) : null;
            if (o == null)
            {
                if (box == null || Time.frameCount - waiting.Since > WaitFrames)
                    Pending.RemoveAt(i);
                continue;
            }
            Pending.RemoveAt(i);
            if (BoxCustomization.Describe(box.OnUnboxingApplyToContents, out byte kind, out string path))
                Send(o.Ref, kind, path, WorldSide.None);
        }
    }

    private static void Send(NetRef box, byte kind, string path, byte except)
    {
        var w = Outbox.Begin(MessageIds.WorldBoxExtra);
        box.Write(w);
        w.Put(kind);
        w.Put(path);
        if (WorldSide.IsHost)
            Outbox.ToPlayers(w, Channel.Events, except);
        else
            Outbox.ToHost(w, Channel.Events);
    }

    private static void OnBox(in Sender from, NetDataReader reader)
    {
        var boxRef = NetRef.Read(reader);
        byte kind = reader.GetByte();
        string path = reader.GetString();
        if (!WorldSide.Running)
            return;
        if (WorldSide.IsHost)
            Send(boxRef, kind, path, from.Slot);
        var o = NetObjects.Find(boxRef);
        if (o == null || !o.Alive || !o.Go.TryGetComponent(out Cardboard_Box box) || box.content == null)
            return;
        // A box made here keeps its own step (it may be the very delegate the shop gave it).
        box.OnUnboxingApplyToContents ??= new BoxCustomization(kind, path).AsDelegate();
    }
}
