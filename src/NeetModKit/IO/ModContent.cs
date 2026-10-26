using System;
using System.Collections.Generic;
using System.IO;
using NeetLoader;
using NeetLoader.Mods;
using NeetLoader.Runtime;

namespace NeetModKit.IO;

/// <summary>
/// A folder any mod can ship for another mod to read, like "phone" for call files or "tv" for
/// videos: Mods/&lt;any mod&gt;/&lt;name&gt;/. The reader is told to load everything again when a
/// file in one of them changes, or a mod with such a folder loads or unloads. Reloads wait for the
/// next frame, so a burst of changes loads once.
/// <code>
/// _calls = ModContent.Watch(this, "phone", LoadCalls, "*.json", "*.ink");
/// public override void OnAllLoaded() => _calls.Load();
/// void LoadCalls() { foreach (var folder in _calls.Folders()) ... }
/// </code>
/// </summary>
public sealed class ModContent
{
    /// <summary>One mod's content folder.</summary>
    public readonly struct Folder
    {
        public Folder(ModInfo mod, string path)
        {
            Mod = mod;
            Path = path;
        }

        public ModInfo Mod { get; }
        public string Path { get; }

        /// <summary>"ModId/folder/file.ext", for messages.</summary>
        public string Display(string file) =>
            Mod.Id + "/" + file.Substring(Mod.Directory.Length).TrimStart('/', '\\').Replace('\\', '/');
    }

    private static readonly List<ModContent> Active = new List<ModContent>();

    private readonly NeetMod _owner;
    private readonly Action _load;
    private readonly string[] _patterns;
    private bool _dirty;
    private bool _watching;

    private ModContent(NeetMod owner, string folderName, Action load, string[] patterns)
    {
        _owner = owner;
        FolderName = folderName;
        _load = load;
        _patterns = patterns;
    }

    public string FolderName { get; }

    /// <summary>
    /// Starts watching. Nothing is loaded until <see cref="Load"/> is called (usually in
    /// OnAllLoaded, so every mod's folder is there).
    /// </summary>
    public static ModContent Watch(NeetMod owner, string folderName, Action load, params string[] patterns)
    {
        var content = new ModContent(owner, folderName, load, patterns);
        Action<ModInfo> changed = mod =>
        {
            // Every mod reports in once at startup; only the ones with this folder matter.
            if (mod != owner.Info && Directory.Exists(Path.Combine(mod.Directory, folderName)))
                content.MarkDirty();
        };
        ModRegistry.ModLoaded += changed;
        ModRegistry.ModUnloading += changed;
        Active.Add(content);
        owner.Track(Disposable.Create(() =>
        {
            ModRegistry.ModLoaded -= changed;
            ModRegistry.ModUnloading -= changed;
            Active.Remove(content);
        }));
        return content;
    }

    /// <summary>The folders of all loaded mods that have one, in load order.</summary>
    public IEnumerable<Folder> Folders()
    {
        foreach (var mod in ModRegistry.Mods)
        {
            if (mod.State != ModState.Loaded)
                continue;
            string path = Path.Combine(mod.Directory, FolderName);
            if (Directory.Exists(path))
                yield return new Folder(mod, path);
        }
    }

    /// <summary>Loads now. The first call also starts watching the files.</summary>
    public void Load()
    {
        _dirty = false;
        try
        {
            _load();
        }
        catch (Exception e)
        {
            _owner.Log.Error($"Loading the {FolderName} folders failed", e);
        }

        // Only after the first load: while the owner is still loading, its own folder isn't listed
        // yet, and the watch would see it appear and load everything twice.
        if (!_watching)
        {
            _watching = true;
            FileWatch.Start(_owner, FolderPaths, MarkDirty, 1f, _patterns);
        }
    }

    /// <summary>Loads again on the next frame.</summary>
    public void MarkDirty() => _dirty = true;

    internal static void TickAll()
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (i < Active.Count && Active[i]._dirty)
                Active[i].Load();
        }
    }

    private IEnumerable<string> FolderPaths()
    {
        foreach (var mod in ModRegistry.Mods)
        {
            if (mod.State == ModState.Loaded)
                yield return Path.Combine(mod.Directory, FolderName);
        }
    }
}
