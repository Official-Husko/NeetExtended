using System.Collections.Generic;
using Coop.Assets;
using Coop.Assets.Media;
using Coop.Assets.Transfer;
using Coop.Session;
using UnityEngine;

namespace Coop.Join;

/// <summary>
/// Client, while joining: gets the files already in the game before asking for the house, so the
/// house loads with everything in it: the session's TV videos and songs (once the host's list has
/// this game's own folders in it), and the custom pictures and tapes, photos, wallpaper and faces
/// the host listed (<see cref="HouseManifest"/>). Files nobody can send, or over the size limit, are
/// skipped. With the setting "Get files before joining" off, the house is asked for at once and the
/// files follow while playing.
/// </summary>
internal static class JoinFiles
{
    private const float Every = 0.25f;

    private static readonly List<AssetId> Wanted = new List<AssetId>();
    private static readonly HashSet<string> WantedKeys = new HashSet<string>();
    private static List<KeyValuePair<AssetId, string>> _manifest;
    private static bool _asked;
    private static bool _started;
    private static float _startedAt;
    private static float _next;

    /// <summary>What the join is waiting for, for the Co-op tab, or "".</summary>
    public static string Status { get; private set; } = "";

    public static void Init()
    {
        CoopSession.Welcomed += Reset;
        CoopSession.Ended += _ => Reset();
    }

    public static void SetManifest(List<KeyValuePair<AssetId, string>> files)
    {
        if (CoopSession.State != SessionState.Joining || _asked)
            return;
        _manifest = files;
        foreach (var file in files)
            FileLabels.SetIfNew(file.Key.FileName, file.Value);
    }

    public static void Tick()
    {
        if (CoopSession.State != SessionState.Joining || _asked || Time.unscaledTime < _next)
            return;
        _next = Time.unscaledTime + Every;
        if (_manifest == null || !MediaSync.HasCurrentList)
        {
            Status = "Getting the list of files in the game...";
            return;
        }
        if (!CoopMod.Config.Value.GetFilesFirst)
        {
            AskForHouse("without waiting for files");
            return;
        }
        if (!_started)
            Start();

        Collect();
        int done = 0;
        long have = 0, total = 0;
        foreach (var id in Wanted)
        {
            var download = Downloads.Find(id.FileName);
            long size = id.SizeKnown ? id.Size : download != null && download.Total > 0 ? download.Total : 0;
            total += size;
            if (LocalFiles.FindToUse(id) != null)
            {
                done++;
                have += size;
            }
            else if (download == null || download.Misses > 0 || !FileTypes.Fits(id))
            {
                done++; // Nobody has it, or it's too big: the house comes without it.
            }
            else
            {
                have += download.Received;
            }
        }
        if (done == Wanted.Count)
        {
            AskForHouse($"with {Wanted.Count} file(s) ({FileSizes.Describe(total)}) in {Time.unscaledTime - _startedAt:0.0} s");
            return;
        }
        Status = $"Getting the files in the game first: {done} of {Wanted.Count} ({FileSizes.Describe(have)} of {FileSizes.Describe(total)})";
    }

    private static void Start()
    {
        _started = true;
        _startedAt = Time.unscaledTime;
        foreach (var file in _manifest)
        {
            if (LocalFiles.FindToUse(file.Key) == null)
                Downloads.Want(file.Key, file.Value, null);
        }
    }

    // Everything of the manifest and the media list this game didn't have when it started.
    private static void Collect()
    {
        foreach (var file in _manifest)
            Add(file.Key);
        foreach (var id in MediaStandIn.Wanted)
            Add(id);
    }

    private static void Add(in AssetId id)
    {
        if (WantedKeys.Add(id.FileName))
            Wanted.Add(id);
    }

    private static void AskForHouse(string how)
    {
        _asked = true;
        Status = "";
        CoopMod.Log.Info($"Files: asking for the house {how}.");
        Handshake.AskForHouse();
    }

    private static void Reset()
    {
        _manifest = null;
        Wanted.Clear();
        WantedKeys.Clear();
        _asked = false;
        _started = false;
        Status = "";
    }
}
