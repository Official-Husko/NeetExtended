using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeetModKit.IO;
using Newtonsoft.Json.Linq;
using TvStations.Tv;

namespace TvStations.Stations;

/// <summary>
/// Reads the "tv" folder of every loaded mod: each video file is a station, and an optional
/// stations.json names them, sets their volume, puts them in place of a game channel, and hides
/// game channels. Videos the list doesn't mention are added after the listed ones, by file name.
/// </summary>
internal static class StationLoader
{
    public const string FolderName = "tv";
    public const string ListFile = "stations.json";

    /// <summary>File types Unity's video player can open on Windows.</summary>
    private static readonly string[] VideoTypes = { ".mp4", ".m4v", ".mov", ".webm", ".ogv", ".avi", ".wmv", ".mpg", ".mpeg", ".asf", ".dv", ".vp8" };

    private static readonly JsonLoadSettings Json = new JsonLoadSettings { CommentHandling = CommentHandling.Ignore };
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
        var videos = Directory.GetFiles(folder.Path, "*.*", SearchOption.AllDirectories)
            .Where(f => VideoTypes.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var list = ReadList(folder);
        if (list != null)
            AddListed(folder, list, videos);

        foreach (string file in videos)
            Loaded.Add(new Station { Name = Path.GetFileNameWithoutExtension(file), File = file, Source = folder.Display(file) });
    }

    private static JObject ReadList(ModContent.Folder folder)
    {
        string path = Path.Combine(folder.Path, ListFile);
        if (!File.Exists(path))
            return null;
        try
        {
            return JObject.Parse(File.ReadAllText(path), Json);
        }
        catch (Exception e)
        {
            ProblemList.Add($"{folder.Display(path)}: {e.Message}");
            return null;
        }
    }

    /// <summary>Stations named in the list, in its order. Their files are taken off <paramref name="videos"/>.</summary>
    private static void AddListed(ModContent.Folder folder, JObject list, List<string> videos)
    {
        string where = folder.Display(Path.Combine(folder.Path, ListFile));

        if (list["hide"] is JArray hide)
        {
            foreach (var channel in hide)
                Hidden.Add(channel.ToString());
        }

        if (!(list["stations"] is JArray stations))
            return;
        for (int i = 0; i < stations.Count; i++)
        {
            if (!(stations[i] is JObject entry) || entry.Value<string>("file") == null)
            {
                ProblemList.Add($"{where}, station {i + 1}: needs a \"file\".");
                continue;
            }

            string file = Path.GetFullPath(Path.Combine(folder.Path, entry.Value<string>("file")));
            int index = videos.FindIndex(v => string.Equals(Path.GetFullPath(v), file, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                ProblemList.Add($"{where}, station {i + 1}: there's no video \"{entry.Value<string>("file")}\" in the tv folder.");
                continue;
            }
            videos.RemoveAt(index);

            Loaded.Add(new Station
            {
                Name = entry.Value<string>("name") ?? Path.GetFileNameWithoutExtension(file),
                File = file,
                Source = folder.Display(file),
                Volume = Math.Max(0f, Math.Min(1f, entry.Value<float?>("volume") ?? 1f)),
                Replaces = entry["replace"]?.ToString(),
            });
        }
    }
}
