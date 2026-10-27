using System.Collections.Generic;
using Coop.Session;
using NeetModKit.Ui.Notifications;
using UnityEngine;

namespace Coop.Assets.Transfer;

/// <summary>
/// "Receiving clip.webm" and "Sending clip.webm to Bob" in the corner of the screen while files
/// move, each with a bar for everything moving in that batch. When nothing is left, the notice says
/// what came or went and goes after a moment. Setting: Show file transfers.
/// </summary>
internal static class TransferNotices
{
    private const float Every = 0.25f;
    private const float Linger = 2.5f;

    private sealed class Batch
    {
        public readonly Dictionary<string, long> Totals = new Dictionary<string, long>();
        public readonly Dictionary<string, long> Moved = new Dictionary<string, long>();
        public readonly HashSet<string> Live = new HashSet<string>();
        public Notice Notice;
        public string OnlyLabel;

        public void Reset()
        {
            Totals.Clear();
            Moved.Clear();
            Live.Clear();
            Notice = null;
            OnlyLabel = null;
        }

        public void Track(string key, long total, long moved)
        {
            Live.Add(key);
            Totals[key] = total;
            Moved[key] = moved;
        }

        // Anything not seen this time is done.
        public void Sweep()
        {
            foreach (string key in new List<string>(Moved.Keys))
            {
                if (!Live.Contains(key))
                    Moved[key] = Totals[key];
            }
        }

        public long Total()
        {
            long sum = 0;
            foreach (long total in Totals.Values)
                sum += total;
            return sum;
        }

        public long Done()
        {
            long sum = 0;
            foreach (long moved in Moved.Values)
                sum += moved;
            return sum;
        }
    }

    private static readonly Batch Receiving = new Batch();
    private static readonly Batch Sending = new Batch();
    private static float _next;

    public static void Tick()
    {
        if (Time.unscaledTime < _next)
            return;
        _next = Time.unscaledTime + Every;
        if (!CoopMod.Config.Value.ShowFileNotices)
        {
            Close(Receiving, null);
            Close(Sending, null);
            return;
        }
        UpdateReceiving();
        UpdateSending();
    }

    public static void Clear()
    {
        Close(Receiving, null);
        Close(Sending, null);
    }

    private static void UpdateReceiving()
    {
        var batch = Receiving;
        batch.Live.Clear();
        Download current = null;
        foreach (var download in Downloads.Active)
        {
            long total = download.Total > 0 ? download.Total : 0;
            batch.Track(download.Key, total, System.Math.Min(download.Received, total));
            if (current == null || (download.Asked && download.Received > 0 && current.Received == 0))
                current = download;
        }
        if (current == null)
        {
            Finish(batch, "Received", got: Arrived(batch));
            return;
        }
        batch.Sweep();
        batch.OnlyLabel = batch.Totals.Count == 1 ? current.Label : null;
        string title = batch.OnlyLabel != null ? "Receiving " + current.Label : $"Receiving {batch.Totals.Count} files";
        string detail = (batch.OnlyLabel != null ? "from " + Who(current.Source) : current.Label) + " · " + Amount(batch);
        Show(batch, title, detail);
    }

    private static void UpdateSending()
    {
        var batch = Sending;
        batch.Live.Clear();
        Uploads.Upload current = null;
        foreach (var upload in Uploads.All)
        {
            batch.Track(upload.Slot + "/" + upload.Key, upload.Total, upload.Acked);
            if (current == null)
                current = upload;
        }
        if (current == null)
        {
            Finish(batch, "Sent", got: batch.Totals.Count);
            return;
        }
        batch.Sweep();
        string label = FileLabels.Of(current.Key);
        batch.OnlyLabel = batch.Totals.Count == 1 ? label + " to " + Who(current.Slot) : null;
        string title = batch.OnlyLabel != null ? "Sending " + label : $"Sending {batch.Totals.Count} files";
        string detail = (batch.OnlyLabel != null ? "to " + Who(current.Slot) : label + " to " + Who(current.Slot)) + " · " + Amount(batch);
        Show(batch, title, detail);
    }

    private static void Show(Batch batch, string title, string detail)
    {
        long total = batch.Total();
        float progress = total > 0 ? (float)batch.Done() / total : 0f;
        if (batch.Notice == null || !batch.Notice.IsOpen)
        {
            if (CoopMod.Instance == null)
                return;
            batch.Notice = Notices.Show(CoopMod.Instance, title, detail, progress);
            return;
        }
        batch.Notice.KeepOpen();
        batch.Notice.Title = title;
        batch.Notice.Detail = detail;
        batch.Notice.Progress = progress;
    }

    private static void Finish(Batch batch, string verb, int got)
    {
        if (batch.Notice == null)
        {
            batch.Reset();
            return;
        }
        int count = batch.Totals.Count;
        string title;
        if (got == 0)
            title = count == 1 && batch.OnlyLabel != null ? "Couldn't get " + batch.OnlyLabel : $"Couldn't get {count} files";
        else if (got < count)
            title = $"{verb} {got} of {count} files";
        else
            title = verb + " " + (count == 1 && batch.OnlyLabel != null ? batch.OnlyLabel : $"{count} files");
        Close(batch, title, got > 0 ? FileSizes.Describe(batch.Total()) : "nobody had it, or it was too big");
    }

    // Files of the batch that are here now (the rest were given up on).
    private static int Arrived(Batch batch)
    {
        int got = 0;
        foreach (string key in batch.Totals.Keys)
        {
            int dot = key.LastIndexOf('.');
            if (dot > 0 && LocalFiles.Find(key.Substring(0, dot), key.Substring(dot)) != null)
                got++;
        }
        return got;
    }

    private static void Close(Batch batch, string title, string detail = "")
    {
        if (batch.Notice != null && batch.Notice.IsOpen)
        {
            if (title != null)
            {
                batch.Notice.Title = title;
                batch.Notice.Detail = detail;
                batch.Notice.Progress = 1f;
                batch.Notice.Close(Linger);
            }
            else
            {
                batch.Notice.Close();
            }
        }
        batch.Reset();
    }

    private static string Amount(Batch batch) => FileSizes.Describe(batch.Done()) + " of " + FileSizes.Describe(batch.Total());

    private static string Who(byte slot)
    {
        if (slot == CoopPlayer.HostSlot)
            return "the host";
        if (slot == Download.NoSource)
            return "whoever has it";
        return CoopSession.FindSlot(slot)?.Name ?? "player " + (slot + 1);
    }
}
