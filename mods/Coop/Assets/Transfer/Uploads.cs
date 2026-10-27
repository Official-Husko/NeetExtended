using System;
using System.Collections.Generic;
using System.IO;
using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Assets.Transfer;

/// <summary>
/// Files this game is sending, in 16 KB chunks on the Bulk lane. Each transfer keeps at most
/// <see cref="Window"/> bytes ahead of what the other side has acknowledged, so nothing piles up in
/// the connection and the snapshot or a world message never waits long behind a video; all of them
/// together stay under the upload limit from the settings. They take turns, a chunk each, smaller
/// files first in every turn.
/// </summary>
internal static class Uploads
{
    internal sealed class Upload
    {
        public byte Slot;
        public uint Transfer;
        public string Key;
        public long Total;
        public long Sent;
        public long Acked;
        public bool SentAny;
        public float LastAck;
        public FileStream Stream;

        public long Left => Total - Sent;
    }

    public const int ChunkBytes = 16 * 1024;
    private const long Window = 256 * 1024;
    private const float AckTimeout = 30f;

    private static readonly List<Upload> Active = new List<Upload>();
    private static readonly byte[] Buffer = new byte[ChunkBytes];
    private static float _budget;

    public static int Count => Active.Count;

    /// <summary>What's being sent now, for the notices.</summary>
    public static IReadOnlyList<Upload> All => Active;

    public static void Init()
    {
        Messages.On(MessageIds.AssetAck, OnAck);
        Messages.On(MessageIds.AssetCancel, OnCancel);
    }

    /// <summary>Starts sending <paramref name="path"/> (this game's copy of <paramref name="key"/>) from <paramref name="offset"/>.</summary>
    public static void Start(byte slot, uint transfer, string key, string path, long offset)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            var old = Active[i];
            if (old.Slot == slot && (old.Transfer == transfer || old.Key == key))
                Drop(i);
        }
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, ChunkBytes * 4);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            CoopMod.Log.Warn($"Files: can't read {Path.GetFileName(path)} to send it: {e.Message}");
            AssetNet.Missing(slot, transfer, AssetNet.Gone);
            return;
        }
        long start = Math.Max(0, Math.Min(offset, stream.Length));
        stream.Seek(start, SeekOrigin.Begin);
        var upload = new Upload
        {
            Slot = slot, Transfer = transfer, Key = key, Total = stream.Length,
            Sent = start, Acked = start, LastAck = Time.unscaledTime, Stream = stream,
        };
        // Smallest first: a face or a photo shouldn't wait behind a film.
        int at = 0;
        while (at < Active.Count && Active[at].Left <= upload.Left)
            at++;
        Active.Insert(at, upload);
    }

    public static void Tick()
    {
        if (Active.Count == 0)
        {
            _budget = 0f;
            return;
        }
        float rate = Mathf.Max(64, CoopMod.Config.Value.UploadKbps) * 1024f;
        // At most a quarter second saved up, so a quiet moment doesn't turn into a burst.
        _budget = Mathf.Min(_budget + rate * Time.unscaledDeltaTime, Mathf.Max(rate * 0.25f, ChunkBytes));
        float now = Time.unscaledTime;

        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (now - Active[i].LastAck > AckTimeout)
                Drop(i); // The other side asks again if it still wants it.
        }

        bool progress = true;
        while (progress && _budget >= ChunkBytes)
        {
            progress = false;
            for (int i = 0; i < Active.Count && _budget >= ChunkBytes; i++)
            {
                var upload = Active[i];
                if (upload.Sent - upload.Acked >= Window || (upload.SentAny && upload.Left <= 0) || !AssetNet.Receives(upload.Slot))
                    continue;
                if (!SendChunk(upload))
                {
                    Drop(i);
                    break;
                }
                progress = true;
            }
        }
    }

    /// <summary>A player left or the session ended: their transfers stop.</summary>
    public static void DropSlot(byte slot)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (Active[i].Slot == slot)
                Drop(i);
        }
    }

    public static void Clear()
    {
        for (int i = Active.Count - 1; i >= 0; i--)
            Drop(i);
    }

    private static bool SendChunk(Upload upload)
    {
        int count = (int)Math.Min(ChunkBytes, upload.Left);
        int read = 0;
        try
        {
            while (read < count)
            {
                int n = upload.Stream.Read(Buffer, read, count - read);
                if (n <= 0)
                    break;
                read += n;
            }
        }
        catch (IOException e)
        {
            CoopMod.Log.Warn($"Files: reading {upload.Key} to send it failed: {e.Message}");
            return false;
        }
        if (read < count)
            return false; // The file got shorter: it changed, so its hash is wrong now.

        var w = Outbox.Begin(MessageIds.AssetChunk);
        w.Put(upload.Transfer);
        w.Put(upload.Total);
        w.Put(upload.Sent);
        w.Put(Buffer, 0, read);
        AssetNet.ToSlot(upload.Slot, w, Channel.Bulk);
        upload.Sent += read;
        upload.SentAny = true;
        _budget -= Math.Max(read, 1024);
        return true;
    }

    private static void OnAck(in Sender from, NetDataReader reader)
    {
        uint transfer = reader.GetUInt();
        long received = reader.GetLong();
        int i = Find(from.Slot, transfer);
        if (i < 0)
            return;
        var upload = Active[i];
        upload.Acked = Math.Max(upload.Acked, Math.Min(received, upload.Sent));
        upload.LastAck = Time.unscaledTime;
        if (upload.Acked >= upload.Total)
            Drop(i);
    }

    private static void OnCancel(in Sender from, NetDataReader reader)
    {
        int i = Find(from.Slot, reader.GetUInt());
        if (i >= 0)
            Drop(i);
    }

    private static int Find(byte slot, uint transfer)
    {
        for (int i = 0; i < Active.Count; i++)
        {
            if (Active[i].Slot == slot && Active[i].Transfer == transfer)
                return i;
        }
        return -1;
    }

    private static void Drop(int index)
    {
        var upload = Active[index];
        Active.RemoveAt(index);
        try { upload.Stream?.Dispose(); }
        catch (IOException) { }
    }
}
