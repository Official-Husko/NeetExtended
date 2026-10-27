using System;
using System.Collections.Generic;
using NeetModKit.IO;
using Newtonsoft.Json.Linq;
using TvStations.Tv;

namespace TvStations.Stations;

/// <summary>
/// Reads the "tv" folder of every loaded mod: each video file is a station, and an optional
/// stations.jsonc names them, sets their volume, puts them in place of a game channel, and hides
/// game channels. Videos the list doesn't mention are added after the listed ones, by file name.
/// </summary>
internal static class StationLoader
{
    public const string FolderName = "tv";
    /// <summary>stations.jsonc (or stations.json) in a tv folder.</summary>
    public const string ListName = "stations";

    /// <summary>File types Unity's video player can open on Windows.</summary>
    private static readonly HashSet<string> VideoTypes = new HashSet<string> { ".mp4", ".m4v", ".mov", ".webm", ".ogv", ".avi", ".wmv", ".mpg", ".mpeg", ".asf", ".dv", ".vp8" };
    private static readonly List<Station> Loaded = new List<Station>();
    private static readonly HashSet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> ProblemList = new List<string>();
    private static ModContent _content;
    private static TvStationsMod _mod;

    public static IReadOnlyList<Station> Stations => Loaded;
    public static IReadOnlyList<string> Problems => ProblemList;

    public static void Init(TvStationsMod mod)
    {
        _mod = mod;
        _content = ModContent.Watch(mod, FolderName, LoadAll, "*");
    }

    public static void Reload() => _content.Load();

    private static void LoadAll()
    {
        Loaded.Clear();
        Hidden.Clear();
        ProblemList.Clear();

        foreach (var folder in _content.Folders())
            LoadFolder(folder);

        _mod.Log.Info($"{Loaded.Count} station(s), {Hidden.Count} game channel(s) hidden" + (ProblemList.Count > 0 ? $", {ProblemList.Count} problem(s):" : "."));
        foreach (string problem in ProblemList)
            _mod.Log.Warn(problem);
        TvChannels.SetStations(Loaded, Hidden);
    }

    private static void LoadFolder(ModContent.Folder folder)
    {
        var videos = MediaFolder.Read(folder, VideoTypes, ListName, "stations", ProblemList, out var list);
        foreach (var video in videos)
        {
            var entry = video.Entry;
            Loaded.Add(new Station
            {
                Name = entry?.Value<string>("name") ?? video.Name,
                File = video.Path,
                Source = video.Source,
                Volume = Math.Max(0f, Math.Min(1f, entry?.Value<float?>("volume") ?? 1f)),
                Replaces = entry?["replace"]?.ToString(),
            });
        }

        if (list?["hide"] is JArray hide)
        {
            foreach (var channel in hide)
                Hidden.Add(channel.ToString());
        }
    }
}
