using System;
using System.Collections.Generic;
using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;

namespace Coop.Assets.Transfer;

/// <summary>
/// Answers requests for files. Clients only talk to the host, so the host is the middle: it serves
/// what it has, and for a file only another client has, it gets it from them first (they offered it
/// when it went into the house) and then sends it on. It fetches every offered file straight away,
/// since what's in the house is needed in the host's game too.
/// </summary>
internal static class Hub
{
    private struct Waiting
    {
        public byte Slot;
        public uint Transfer;
        public long Offset;
    }

    private static readonly Dictionary<string, byte> OfferedBy = new Dictionary<string, byte>(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<Waiting>> Waits = new Dictionary<string, List<Waiting>>(StringComparer.Ordinal);

    public static void Init()
    {
        Messages.On(MessageIds.AssetWant, OnWant);
        Messages.On(MessageIds.AssetOffer, OnOffer);
    }

    /// <summary>Host: who to ask for a file, or <see cref="Download.NoSource"/>.</summary>
    public static byte SourceOf(string key) => OfferedBy.TryGetValue(key, out byte slot) ? slot : Download.NoSource;

    /// <summary>
    /// Host: a client has this file (it offered it, or put it in the house or on its face). The host
    /// gets it now.
    /// </summary>
    public static void Offered(in AssetId id, byte slot, string label)
    {
        if (!CoopSession.IsHost || !id.IsValid || slot == CoopPlayer.HostSlot)
            return;
        OfferedBy[id.FileName] = slot;
        Downloads.SourceAppeared(id.FileName);
        Downloads.Want(id, label, null);
    }

    /// <summary>Client: tells the host this game has a file it put in the house.</summary>
    public static void Offer(in AssetId id)
    {
        if (!CoopSession.IsClient || !id.IsValid)
            return;
        var w = Outbox.Begin(MessageIds.AssetOffer);
        id.Write(w);
        Outbox.ToHost(w, Channel.Events);
    }

    public static void SlotLeft(byte slot)
    {
        var gone = new List<string>();
        foreach (var pair in OfferedBy)
        {
            if (pair.Value == slot)
                gone.Add(pair.Key);
        }
        foreach (string key in gone)
            OfferedBy.Remove(key);
        foreach (var list in Waits.Values)
            list.RemoveAll(w => w.Slot == slot);
    }

    public static void Clear()
    {
        OfferedBy.Clear();
        Waits.Clear();
    }

    private static void OnOffer(in Sender from, NetDataReader reader)
    {
        var id = AssetId.Read(reader);
        if (id.IsValid && id.SizeKnown)
            Offered(id, from.Slot, "a file from " + (CoopSession.FindSlot(from.Slot)?.Name ?? "a player"));
    }

    private static void OnWant(in Sender from, NetDataReader reader)
    {
        uint transfer = reader.GetUInt();
        var id = AssetId.Read(reader);
        long offset = reader.GetLong();
        if (!id.IsValid || offset < 0)
        {
            AssetNet.Missing(from.Slot, transfer, AssetNet.Gone);
            return;
        }

        string path = LocalFiles.Find(id);
        if (path != null)
        {
            if (new System.IO.FileInfo(path).Length > FileTypes.MaxBytes(id.Ext))
                AssetNet.Missing(from.Slot, transfer, AssetNet.TooBig);
            else
                Uploads.Start(from.Slot, transfer, id.FileName, path, offset);
            return;
        }

        // Host: another client has it, or it's on its way here already.
        if (CoopSession.IsHost && (Downloads.IsGetting(id.FileName) || SourceOf(id.FileName) != Download.NoSource))
        {
            if (!Waits.TryGetValue(id.FileName, out var list))
                Waits[id.FileName] = list = new List<Waiting>();
            byte slot = from.Slot;
            list.RemoveAll(w => w.Slot == slot);
            list.Add(new Waiting { Slot = slot, Transfer = transfer, Offset = offset });
            string key = id.FileName;
            Downloads.Want(id, "a file for " + (CoopSession.FindSlot(from.Slot)?.Name ?? "a player"), local => ServeWaiting(key, local));
            AssetNet.Missing(from.Slot, transfer, AssetNet.Coming);
            return;
        }
        AssetNet.Missing(from.Slot, transfer, AssetNet.Gone);
    }

    private static void ServeWaiting(string key, string path)
    {
        if (!Waits.TryGetValue(key, out var list))
            return;
        Waits.Remove(key);
        foreach (var waiting in list)
        {
            if (CoopSession.FindSlot(waiting.Slot) != null)
                Uploads.Start(waiting.Slot, waiting.Transfer, key, path, waiting.Offset);
        }
    }
}
