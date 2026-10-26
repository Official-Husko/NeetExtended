using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeetLoader.Mods;
using NeetLoader.Runtime;
using NeetModKit.Conversations;
using NeetModKit.IO;
using NeetModKit.Registry;

namespace PhoneExpansion.Packs;

/// <summary>
/// Loads call files from the "phone" folder of every loaded mod (this one included): .ink scripts
/// and .json call files. Everything is loaded again when a file changes or a mod comes or goes.
/// A mod made only of a mod.json and a phone folder is a complete call pack.
/// </summary>
internal static class PackLoader
{
    public const string FolderName = "phone";

    private static readonly List<string> LoadProblems = new List<string>();
    private static readonly List<InkScript> Scripts = new List<InkScript>();
    private static PhoneExpansionMod _mod;
    private static Scope _scope;
    private static bool _dirty;
    private static bool _watching;

    public static int FileCount { get; private set; }

    /// <summary>File errors plus any compile errors of the generated or hand-written Ink.</summary>
    public static List<string> Problems()
    {
        var all = new List<string>(LoadProblems);
        foreach (var script in Scripts)
        {
            foreach (string problem in script.Problems)
                all.Add($"{script.Name}: {problem}");
        }
        return all;
    }

    public static void Init(PhoneExpansionMod mod)
    {
        _mod = mod;
        _watching = false;
        // Only mods with a phone folder matter; at startup every mod reports in once.
        Action<ModInfo> changed = info =>
        {
            if (info.Id != mod.Info.Id && Directory.Exists(Path.Combine(info.Directory, FolderName)))
                MarkDirty();
        };
        ModRegistry.ModLoaded += changed;
        ModRegistry.ModUnloading += changed;
        mod.Track(Disposable.Create(() =>
        {
            ModRegistry.ModLoaded -= changed;
            ModRegistry.ModUnloading -= changed;
        }));
    }

    public static void MarkDirty() => _dirty = true;

    public static void Tick()
    {
        if (!_dirty)
            return;
        _dirty = false;
        Reload();
    }

    public static void Reload()
    {
        _dirty = false;
        _scope?.Dispose();
        _scope = new Scope(_mod);
        LoadProblems.Clear();
        Scripts.Clear();
        FileCount = 0;

        foreach (var mod in ModRegistry.Mods)
        {
            if (mod.State != ModState.Loaded)
                continue;
            string folder = Path.Combine(mod.Directory, FolderName);
            if (Directory.Exists(folder))
                LoadFolder(mod, folder);
        }

        // Started after the first load: while this mod is still loading, its own folder isn't
        // listed yet, and the watch would see it appear and load everything twice.
        if (!_watching)
        {
            FileWatch.Start(_mod, Folders, MarkDirty, 1f, "*.json", "*.ink");
            _watching = true;
        }

        var problems = Problems();
        _mod.Log.Info($"Loaded {Calls.PhoneBook.All.Count} calls from {FileCount} files" +
                      (problems.Count > 0 ? $", {problems.Count} problem(s):" : "."));
        foreach (string problem in problems)
            _mod.Log.Warn(problem);
    }

    private static void LoadFolder(ModInfo mod, string folder)
    {
        var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        // Scripts first, so calls in .json files can point at their knots.
        foreach (string file in files.Where(f => f.EndsWith(".ink", StringComparison.OrdinalIgnoreCase)))
        {
            FileCount++;
            try { Scripts.Add(InkScripts.Add(_scope, Display(mod, file), File.ReadAllText(file))); }
            catch (IOException e) { LoadProblems.Add($"{Display(mod, file)}: {e.Message}"); }
        }

        foreach (string file in files.Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            FileCount++;
            CallParser.LoadFile(file, Display(mod, file), mod.Id, _scope, LoadProblems, Scripts);
        }
    }

    private static IEnumerable<string> Folders()
    {
        foreach (var mod in ModRegistry.Mods)
        {
            if (mod.State == ModState.Loaded)
                yield return Path.Combine(mod.Directory, FolderName);
        }
    }

    private static string Display(ModInfo mod, string file) =>
        mod.Id + "/" + file.Substring(mod.Directory.Length).TrimStart('/', '\\').Replace('\\', '/');
}
