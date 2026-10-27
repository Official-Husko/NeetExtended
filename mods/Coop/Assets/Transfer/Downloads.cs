using System;
using System.Collections.Generic;
using Coop.Net;
using Coop.Session;
using UnityEngine;

namespace Coop.Assets.Transfer;

/// <summary>
/// Files this game is getting. <see cref="Want"/> hands over the local path once the file is here;
/// until then whatever asked shows what the game shows without it. Each file is asked for once, from
/// the host (on a client) or from the player who offered it (on the host), and resumed after a hot
/// reload or a silence. A finished file is checked against its hash before anything uses it.
/// </summary>
internal static partial class Downloads
{
    private const float Silence = 20f;
    private const float RetryLater = 30f;
    private const int AckEvery = 64 * 1024;
    private const int MaxFailures = 3;
    // Asked every RetryLater seconds: five minutes of nobody having it.
    private const int MaxMisses = 10;
    private const long LogAbove = 1024 * 1024;

    private static readonly Dictionary<string, Download> ByKey = new Dictionary<string, Download>(StringComparer.Ordinal);
    private static readonly Dictionary<uint, Download> ByTransfer = new Dictionary<uint, Download>();
    private static readonly List<Download> All = new List<Download>();
    private static readonly List<Verifier.Result> Checked = new List<Verifier.Result>();
    private static readonly HashSet<string> ToldTooBig = new HashSet<string>();
    private static uint _nextTransfer = (uint)new System.Random().Next();

    public static IReadOnlyList<Download> Active => All;

    /// <summary>A file arrived and is in the cache (after its waiters ran).</summary>
    public static event Action<AssetId> Arrived;

    public static void Init()
    {
        Messages.On(MessageIds.AssetChunk, OnChunk);
        Messages.On(MessageIds.AssetMissing, OnMissing);
    }

    /// <summary>
    /// Calls <paramref name="ready"/> with this game's copy of the file: now if it's here, else when
    /// it arrives (only in a session). Files over this game's size limit never come.
    /// </summary>
    public static void Want(AssetId id, string label, Action<string> ready)
    {
        if (!id.IsValid)
            return;
        string local = LocalFiles.FindToUse(id);
        if (local != null)
        {
            ready?.Invoke(local);
            return;
        }
        if (!AssetNet.CanTalk)
            return;
        if (!FileTypes.Fits(id))
        {
            if (ToldTooBig.Add(id.Hash))
                CoopMod.Log.Info($"Files: not getting {label} ({FileSizes.Describe(id.Size)}): over the {CoopMod.Config.Value.MaxFileMb} MB limit in the Co-op settings.");
            return;
        }
        if (!ByKey.TryGetValue(id.FileName, out var download))
        {
            download = new Download { Id = id, Label = label, Transfer = _nextTransfer++, Total = id.Size };
            FileLabels.SetIfNew(id.FileName, label);
            ByKey[download.Key] = download;
            ByTransfer[download.Transfer] = download;
            All.Add(download);
            Ask(download);
        }
        else
        {
            if (download.Total < 0 && id.SizeKnown)
                download.Total = id.Size;
            // Asked for again (a list or an item names it once more): maybe someone has it now.
            if (!download.Asked && !download.Verifying)
                download.RetryAt = 0f;
        }
        if (ready != null)
            download.Waiters.Add(ready);
    }

    public static bool IsGetting(string key) => ByKey.ContainsKey(key);

    /// <summary>The download of "hash.ext", or null.</summary>
    public static Download Find(string key) => ByKey.TryGetValue(key, out var download) ? download : null;

    /// <summary>Host: someone offered a file; a download nobody had a source for asks them now.</summary>
    public static void SourceAppeared(string key)
    {
        if (ByKey.TryGetValue(key, out var download) && !download.Asked && !download.Verifying)
            download.RetryAt = 0f;
    }

    /// <summary>A player left: what they were sending is asked for elsewhere (or later).</summary>
    public static void SourceLeft(byte slot)
    {
        foreach (var download in All)
        {
            if (download.Source != slot || download.Verifying)
                continue;
            download.Close();
            download.Asked = false;
            download.Source = Download.NoSource;
            download.RetryAt = Time.unscaledTime + 1f;
        }
    }

    /// <summary>The player sending these hot reloaded: ask again on the next tick.</summary>
    private static void AskAgain(byte slot)
    {
        foreach (var download in All)
        {
            if (download.Source == slot && download.Asked && !download.Verifying)
                download.LastHeard = float.NegativeInfinity;
        }
    }

    public static void Tick()
    {
        Verifier.Drain(Checked);
        foreach (var result in Checked)
            Finished(result);
        Checked.Clear();

        float now = Time.unscaledTime;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var download = All[i];
            if (download.Verifying)
                continue;
            if (!download.Asked ? now >= download.RetryAt : now - download.LastHeard > Silence)
                Ask(download);
        }
    }

    /// <summary>Stops everything (session over, or the mod unloads); parts stay for next time.</summary>
    public static void Clear()
    {
        foreach (var download in All)
            download.Close();
        All.Clear();
        ByKey.Clear();
        ByTransfer.Clear();
    }

    /// <summary>Parts still being written, so the cache doesn't tidy them away.</summary>
    public static HashSet<string> Parts()
    {
        var parts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var download in All)
            parts.Add(download.PartPath);
        return parts;
    }

    private static void Ask(Download download)
    {
        download.Asked = false;
        download.Source = CoopSession.IsClient ? CoopPlayer.HostSlot : Hub.SourceOf(download.Key);
        if (download.Source == Download.NoSource || !AssetNet.CanTalk)
        {
            download.RetryAt = Time.unscaledTime + RetryLater;
            return;
        }
        try
        {
            download.Open();
        }
        catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
        {
            CoopMod.Log.Warn($"Files: can't write {download.PartPath}: {e.Message}");
            download.RetryAt = Time.unscaledTime + RetryLater;
            return;
        }
        var w = Outbox.Begin(MessageIds.AssetWant);
        w.Put(download.Transfer);
        download.Id.Write(w);
        w.Put(download.Received);
        AssetNet.ToSlot(download.Source, w, Channel.Events);
        download.Asked = true;
        download.LastHeard = Time.unscaledTime;
        if (download.StartedAt <= 0f)
        {
            download.StartedAt = Time.unscaledTime;
            download.StartedFrom = download.Received;
        }
    }
}
