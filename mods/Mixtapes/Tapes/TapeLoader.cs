using System;
using System.Collections.Generic;
using System.Globalization;
using Mixtapes.Audio;
using NeetModKit.Game;
using NeetModKit.IO;
using NeetModKit.Registry;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Mixtapes.Tapes;

/// <summary>
/// Reads the "mixtapes" folder of every loaded mod: each song file is a tape, and an optional
/// tapes.jsonc names them, prices them, colours them and leaves them in a room. Songs the list
/// doesn't mention follow the listed ones, by file name. Everything loads again when a file changes.
/// </summary>
internal static class TapeLoader
{
    public const string FolderName = "mixtapes";
    /// <summary>tapes.jsonc (or tapes.json) in a mixtapes folder.</summary>
    public const string ListName = "tapes";

    private static readonly List<Tape> Loaded = new List<Tape>();
    private static readonly List<string> ProblemList = new List<string>();
    private static ModContent _content;
    private static Scope _scope;
    private static MixtapesMod _mod;

    public static IReadOnlyList<Tape> Tapes => Loaded;
    public static IReadOnlyList<string> Problems => ProblemList;

    /// <summary>Raised after every load.</summary>
    public static event Action Changed;

    public static void Init(MixtapesMod mod)
    {
        _mod = mod;
        _content = ModContent.Watch(mod, FolderName, LoadAll, "*");
    }

    public static void Reload() => _content.Load();

    private static void LoadAll()
    {
        _scope?.Dispose();
        _scope = new Scope(_mod);
        Loaded.Clear();
        ProblemList.Clear();

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in _content.Folders())
        {
            foreach (var song in MediaFolder.Read(folder, SongLibrary.Types, ListName, "tapes", ProblemList, out _))
            {
                var tape = Read(song);
                if (tape == null)
                    continue;
                if (!names.Add(tape.Name))
                {
                    ProblemList.Add($"{song.Source}: there's already a tape called \"{tape.Name}\". Give it another \"name\".");
                    continue;
                }
                Loaded.Add(tape);
                files.Add(tape.File);
                TapeItems.Register(_scope, tape);
                SongLibrary.Get(tape.File, _ => { }); // Load it now, before a save needs it.
            }
        }
        SongLibrary.Keep(files);

        _mod.Log.Info($"{Loaded.Count} tape(s)" + (ProblemList.Count > 0 ? $", {ProblemList.Count} problem(s):" : "."));
        foreach (string problem in ProblemList)
            _mod.Log.Warn(problem);
        Changed?.Invoke();
    }

    private static Tape Read(MediaFile song)
    {
        var entry = song.Entry;
        var tape = new Tape
        {
            Name = (entry?.Value<string>("name") ?? song.Name).Trim(),
            File = song.Path,
            Source = song.Source,
            Description = entry?.Value<string>("description"),
            Price = Mathf.Max(0f, entry?.Value<float?>("price") ?? MixtapesMod.Config.Value.Price),
            Sold = entry?.Value<bool?>("store") ?? true,
        };
        try
        {
            string room = entry?.Value<string>("house");
            if (room != null)
            {
                Rooms.Parse(room); // Checks the name.
                tape.Room = room;
            }
            if (entry?["color"] != null)
                tape.Color = ParseColor(entry["color"]);
        }
        catch (Exception e) when (e is ArgumentException || e is FormatException)
        {
            ProblemList.Add($"{song.Source}: {e.Message}");
            return null;
        }
        return tape;
    }

    /// <summary>"#ff8800" or [1, 0.5, 0].</summary>
    private static Color ParseColor(JToken value)
    {
        if (value is JArray rgb && rgb.Count >= 3)
            return new Color(rgb[0].Value<float>(), rgb[1].Value<float>(), rgb[2].Value<float>());
        string text = value.ToString();
        if (ColorUtility.TryParseHtmlString(text.StartsWith("#", StringComparison.Ordinal) ? text : "#" + text, out var color))
            return color;
        throw new FormatException($"\"color\" should look like \"#ff8800\", not '{text.ToString(CultureInfo.InvariantCulture)}'.");
    }
}
