using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private static ModContent _content;
    private static Scope _scope;

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
        _content = ModContent.Watch(mod, FolderName, LoadAll, "*.json", "*.ink");
    }

    /// <summary>Loads every call file again, now.</summary>
    public static void Reload() => _content.Load();

    private static void LoadAll()
    {
        _scope?.Dispose();
        _scope = new Scope(_mod);
        LoadProblems.Clear();
        Scripts.Clear();
        FileCount = 0;

        foreach (var folder in _content.Folders())
            LoadFolder(folder);

        var problems = Problems();
        _mod.Log.Info($"Loaded {Calls.PhoneBook.All.Count} calls from {FileCount} files" +
                      (problems.Count > 0 ? $", {problems.Count} problem(s):" : "."));
        foreach (string problem in problems)
            _mod.Log.Warn(problem);
    }

    private static void LoadFolder(ModContent.Folder folder)
    {
        var files = Directory.GetFiles(folder.Path, "*.*", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        // Scripts first, so calls in .json files can point at their knots.
        foreach (string file in files.Where(f => f.EndsWith(".ink", StringComparison.OrdinalIgnoreCase)))
        {
            FileCount++;
            try { Scripts.Add(InkScripts.Add(_scope, folder.Display(file), File.ReadAllText(file))); }
            catch (IOException e) { LoadProblems.Add($"{folder.Display(file)}: {e.Message}"); }
        }

        foreach (string file in files.Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            FileCount++;
            CallParser.LoadFile(file, folder.Display(file), folder.Mod.Id, _scope, LoadProblems, Scripts);
        }
    }
}
