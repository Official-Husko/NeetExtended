using System.Collections.Generic;
using Coop.Net;
using Coop.Session;
using Coop.World;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Assets.Items;

/// <summary>
/// A file on something in the house that changed after it was shared: a photo that was just taken,
/// a custom item or the wallpaper the host turned into a shared file when it started hosting, a
/// wallpaper someone picked. The others put the shared path on their copy and load it.
///
/// AssetPath (Events, clients to the host, which passes it on): kind, item ref (not for the
/// wallpaper), shared path. Only shared paths are taken.
/// </summary>
internal static class ItemPaths
{
    public const byte Texture = 0;
    public const byte Music = 1;
    public const byte Photo = 2;
    public const byte Wallpaper = 3;

    // A photo is shared a frame or two after it's taken; something sent for an item not here yet waits.
    private const float WaitSeconds = 5f;

    private struct Outgoing
    {
        public Component Part;
        public byte Kind;
        public string Path;
        public float Until;
    }

    private struct Incoming
    {
        public NetRef Ref;
        public byte Kind;
        public string Path;
        public float Until;
    }

    private static readonly List<Outgoing> ToSend = new List<Outgoing>();
    private static readonly List<Incoming> ToApply = new List<Incoming>();

    public static void Init() => Messages.On(MessageIds.AssetPath, OnPath);

    public static void Clear()
    {
        ToSend.Clear();
        ToApply.Clear();
    }

    /// <summary>Tells the others about an item's new shared file, once the item is shared.</summary>
    public static void Announce(Component part, byte kind, string path)
    {
        if (CoopSession.IsActive && part != null && SharedPaths.IsShared(path))
            ToSend.Add(new Outgoing { Part = part, Kind = kind, Path = path, Until = Time.unscaledTime + WaitSeconds });
    }

    public static void AnnounceWallpaper(string path)
    {
        if (!CoopSession.IsActive || !SharedPaths.IsShared(path))
            return;
        var w = Outbox.Begin(MessageIds.AssetPath);
        w.Put(Wallpaper);
        w.Put(path);
        Outbox.ToPlayers(w, Channel.Events);
    }

    /// <summary>Late in the frame, after the frame's spawns went out.</summary>
    public static void LateTick()
    {
        if (ToSend.Count == 0 && ToApply.Count == 0)
            return;
        float now = Time.unscaledTime;
        for (int i = ToSend.Count - 1; i >= 0; i--)
        {
            var item = ToSend[i];
            var o = item.Part != null && WorldSide.Running ? NetObjects.Find(ItemOf(item.Part)) : null;
            if (o != null)
                Send(o.Ref, item.Kind, item.Path);
            if (o != null || item.Part == null || now > item.Until)
                ToSend.RemoveAt(i);
        }
        for (int i = ToApply.Count - 1; i >= 0; i--)
        {
            var item = ToApply[i];
            var o = WorldSide.Running ? NetObjects.Find(item.Ref) : null;
            if (o != null && o.Alive)
                ItemFiles.Apply(o.Go, item.Kind, item.Path);
            if (o != null || now > item.Until)
                ToApply.RemoveAt(i);
        }
    }

    private static GameObject ItemOf(Component part)
    {
        var usable = part.GetComponentInParent<Usable>();
        return usable != null ? usable.gameObject : part.gameObject;
    }

    private static void Send(NetRef item, byte kind, string path)
    {
        var w = Outbox.Begin(MessageIds.AssetPath);
        w.Put(kind);
        item.Write(w);
        w.Put(path);
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void OnPath(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        byte kind = reader.GetByte();
        var item = kind == Wallpaper ? default : NetRef.Read(reader);
        string path = reader.GetString(1024);
        if (!SharedPaths.IsShared(path))
            return;
        if (CoopSession.IsHost)
        {
            // The sender has the file; the others get the news as it came.
            Relay.Forward(MessageIds.AssetPath, reader.RawData, start, length, Channel.Events, from.Slot);
            ItemFiles.OfferedBy(path, from.Slot);
        }

        if (kind == Wallpaper)
        {
            ItemFiles.ApplyWallpaper(path);
            return;
        }
        var o = WorldSide.Running ? NetObjects.Find(item) : null;
        if (o != null && o.Alive)
            ItemFiles.Apply(o.Go, kind, path);
        else
            ToApply.Add(new Incoming { Ref = item, Kind = kind, Path = path, Until = Time.unscaledTime + WaitSeconds });
    }
}
