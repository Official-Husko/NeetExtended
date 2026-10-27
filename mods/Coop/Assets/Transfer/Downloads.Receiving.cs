using System;
using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Assets.Transfer;

/// <summary>What comes back for the files this game asked for: chunks, "nobody has it", and the finished check.</summary>
internal static partial class Downloads
{
    private static void OnChunk(in Sender from, NetDataReader reader)
    {
        uint transfer = reader.GetUInt();
        long total = reader.GetLong();
        long offset = reader.GetLong();
        if (!ByTransfer.TryGetValue(transfer, out var download) || download.Verifying || !download.Asked || from.Slot != download.Source)
            return;
        if (total < 0 || total > FileTypes.MaxBytes(download.Id.Ext) || (download.Total >= 0 && total != download.Total))
        {
            Fail(download, $"it's {FileSizes.Describe(total)}, not what was expected or over the limit", giveUp: true);
            Cancel(download);
            return;
        }
        if (download.Total < 0)
        {
            download.Total = total;
            if (download.Received > total)
            {
                download.Truncate();
                Ask(download);
                return;
            }
        }
        if (offset != download.Received)
            return; // From an earlier ask; the answer to the latest one follows.
        int count = reader.AvailableBytes;
        if (download.Received + count > download.Total)
        {
            Fail(download, "more bytes came than the file has", giveUp: false);
            return;
        }
        if (!download.Announced && download.Total >= LogAbove)
        {
            download.Announced = true;
            CoopMod.Log.Info($"Files: getting {download.Describe()} from {Who(download.Source)}" + (download.Received > 0 ? $", from {FileSizes.Describe(download.Received)} on." : "."));
        }
        if (count > 0)
            download.Write(reader.RawData, reader.Position, count);
        download.LastHeard = Time.unscaledTime;
        if (download.Received - download.AckedAt >= AckEvery || download.Received == download.Total)
            Ack(download);
        if (download.Received == download.Total)
        {
            download.Close();
            download.Verifying = true;
            Verifier.Check(download);
        }
    }

    private static void OnMissing(in Sender from, NetDataReader reader)
    {
        uint transfer = reader.GetUInt();
        byte reason = reader.GetByte();
        if (reason == AssetNet.Restarted)
        {
            AskAgain(from.Slot);
            return;
        }
        if (!ByTransfer.TryGetValue(transfer, out var download) || from.Slot != download.Source || download.Verifying)
            return;
        if (reason == AssetNet.Coming)
        {
            download.LastHeard = Time.unscaledTime;
            return;
        }
        download.Close();
        download.Asked = false;
        if (reason == AssetNet.TooBig)
        {
            Fail(download, $"{Who(download.Source)} doesn't send files that big", giveUp: true);
            return;
        }
        if (++download.Misses >= MaxMisses)
        {
            CoopMod.Log.Info($"Files: nobody in the game has {download.Describe()}; stopped asking.");
            Remove(download);
            return;
        }
        download.RetryAt = Time.unscaledTime + RetryLater;
    }

    private static void Finished(in Verifier.Result result)
    {
        var download = result.Download;
        if (!ByKey.TryGetValue(download.Key, out var current) || current != download)
            return; // Cleared meanwhile; the file is in the cache all the same if it was good.
        download.Verifying = false;
        if (!result.Good)
        {
            Fail(download, result.Error, giveUp: false);
            return;
        }
        Remove(download);
        string path = FileCache.PathOf(download.Id);
        float seconds = Mathf.Max(0.01f, Time.unscaledTime - download.StartedAt);
        if (download.Total >= LogAbove || download.Announced)
            CoopMod.Log.Info($"Files: got {download.Describe()} in {seconds:0.0} s ({(download.Total - download.StartedFrom) / 1024f / 1024f / seconds:0.0} MB/s).");
        else
            CoopMod.Log.Debug($"Files: got {download.Describe()}.");
        foreach (var ready in download.Waiters)
        {
            try { ready(path); }
            catch (Exception e) { CoopMod.Log.Error($"Files: using {download.Label} failed: {e}"); }
        }
        var id = new AssetId(download.Id.Hash, download.Total, download.Id.Ext);
        try { Arrived?.Invoke(id); }
        catch (Exception e) { CoopMod.Log.Error($"Files: after {download.Label} arrived: {e}"); }
        FileCache.Trim();
    }

    private static void Fail(Download download, string why, bool giveUp)
    {
        download.DeletePart();
        download.Asked = false;
        download.Failures++;
        if (giveUp || download.Failures >= MaxFailures)
        {
            CoopMod.Log.Warn($"Files: gave up on {download.Describe()}: {why}.");
            Remove(download);
            return;
        }
        CoopMod.Log.Warn($"Files: {download.Describe()} has to start over: {why}.");
        // A fresh transfer id, so nothing still on its way for the old one is taken.
        ByTransfer.Remove(download.Transfer);
        download.Transfer = _nextTransfer++;
        ByTransfer[download.Transfer] = download;
        download.Total = download.Id.Size;
        download.RetryAt = 0f;
    }

    private static void Remove(Download download)
    {
        download.Close();
        ByKey.Remove(download.Key);
        ByTransfer.Remove(download.Transfer);
        All.Remove(download);
    }

    private static void Ack(Download download)
    {
        download.AckedAt = download.Received;
        var w = Outbox.Begin(MessageIds.AssetAck);
        w.Put(download.Transfer);
        w.Put(download.Received);
        AssetNet.ToSlot(download.Source, w, Channel.Events);
    }

    private static void Cancel(Download download)
    {
        var w = Outbox.Begin(MessageIds.AssetCancel);
        w.Put(download.Transfer);
        AssetNet.ToSlot(download.Source, w, Channel.Events);
    }

    private static string Who(byte slot)
    {
        if (slot == CoopPlayer.HostSlot)
            return "the host";
        var player = CoopSession.FindSlot(slot);
        return player != null ? player.Name : "player " + slot;
    }
}
