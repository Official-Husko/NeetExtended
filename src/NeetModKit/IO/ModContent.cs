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
/// _calls = ModContent.Watch(this, "phone", LoadCalls, "*.jsonc", "*.json", "*.ink");
/// public override void OnAllLoaded() => _calls.Load();
/// void LoadCalls() { foreach (var folder in _calls.Folders()) ... }
/// </code>
/// </summary>
public sealed class ModContent
{
    /// <summary>One mod's content folder.</summary>
    public readonly struct Folder
    {
        private readonly Dictionary<string, string> _real;

        public Folder(ModInfo mod, string path)
        {
            Mod = mod;
            Path = path;
            Files = null;
            _real = null;
        }

        /// <summary>
        /// A folder whose files are somewhere else (another game's folder, copied over by co-op).
        /// <paramref name="path"/> is where it would be in this game; <paramref name="files"/> maps each
        /// file's path relative to it ("clip.webm", "stations.jsonc") to where that file really is.
        /// </summary>
        public Folder(ModInfo mod, string path, IReadOnlyDictionary<string, string> files)
        {
            Mod = mod;
            Path = path;
            Files = files;
            _real = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
                _real[System.IO.Path.GetFullPath(System.IO.Path.Combine(path, file.Key))] = file.Value;
        }

        public ModInfo Mod { get; }
        public string Path { get; }

        /// <summary>For a folder made of files from elsewhere: relative path to real path. Null for a folder on disk.</summary>
        public IReadOnlyDictionary<string, string> Files { get; }

        public bool IsVirtual => Files != null;

        /// <summary>Where a file of this folder really is: itself on disk, the mapped file for a virtual folder.</summary>
        public string RealPath(string file) =>
            _real != null && _real.TryGetValue(System.IO.Path.GetFullPath(file), out string real) ? real : file;

        /// <summary>"ModId/folder/file.ext", for messages.</summary>
        public string Display(string file) => file.StartsWith(Mod.Directory, StringComparison.OrdinalIgnoreCase)
            ? Mod.Id + "/" + file.Substring(Mod.Directory.Length).TrimStart('/', '\\').Replace('\\', '/')
            : Mod.Id + "/" + System.IO.Path.GetFileName(file);
    }

    private sealed class Stand
    {
        public string OwnerId;
        public IReadOnlyList<Folder> Folders;
    }

    private static readonly List<ModContent> Active = new List<ModContent>();
    private static readonly Dictionary<string, Stand> Stands = new Dictionary<string, Stand>(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>
    /// Shows <paramref name="folders"/> in place of every mod's own folders of this name, until it's
    /// called again with null or <paramref name="owner"/> unloads for good. Readers load again on the
    /// next frame (not when the same folders are set again). Co-op uses it so a joining player's TV and
    /// tapes are the host's videos and songs.
    /// </summary>
    public static void Substitute(NeetMod owner, string folderName, IReadOnlyList<Folder> folders)
    {
        if (folders == null)
        {
            if (!Stands.Remove(folderName))
                return;
        }
        else
        {
            if (Stands.TryGetValue(folderName, out var stand) && stand.OwnerId == owner.Info.Id && SameFolders(stand.Folders, folders))
                return;
            Stands[folderName] = new Stand { OwnerId = owner.Info.Id, Folders = folders };
        }
        MarkDirty(folderName);
    }

    /// <summary>Loads every reader of this folder name again on the next frame (files of a stand-in arrived).</summary>
    public static void MarkDirty(string folderName)
    {
        foreach (var content in Active)
        {
            if (string.Equals(content.FolderName, folderName, StringComparison.OrdinalIgnoreCase))
                content.MarkDirty();
        }
    }

    /// <summary>
    /// A file a stand-in folder lists has just turned up (co-op got it), with its real path. Readers
    /// that kept an entry for the missing file can start using it now, without loading the whole
    /// folder again: the TV plays the station, the tape gets its song.
    /// </summary>
    public static event Action<string> FileArrived;

    /// <summary>Raises <see cref="FileArrived"/>; a listener that throws doesn't stop the others.</summary>
    public static void ReportArrived(string path)
    {
        var handlers = FileArrived;
        if (handlers == null || string.IsNullOrEmpty(path))
            return;
        foreach (Action<string> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(path);
            }
            catch (Exception e)
            {
                KitMod.Instance?.Log.Error($"Using {Path.GetFileName(path)} after it arrived failed", e);
            }
        }
    }

    /// <summary>The folders of all loaded mods that have one, in load order (or their stand-ins).</summary>
    public IEnumerable<Folder> Folders()
    {
        if (Stands.TryGetValue(FolderName, out var stand))
        {
            foreach (var folder in stand.Folders)
                yield return folder;
            yield break;
        }
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
        if (Stands.Count > 0)
            DropOrphanedStands();
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (i < Active.Count && Active[i]._dirty)
                Active[i].Load();
        }
    }

    // A stand-in whose mod is gone (switched off rather than reloaded) gives the folders back.
    private static void DropOrphanedStands()
    {
        string orphan = null;
        foreach (var pair in Stands)
        {
            if (!ModRegistry.IsLoaded(pair.Value.OwnerId))
            {
                orphan = pair.Key;
                break;
            }
        }
        if (orphan == null)
            return;
        Stands.Remove(orphan);
        MarkDirty(orphan);
    }

    private static bool SameFolders(IReadOnlyList<Folder> a, IReadOnlyList<Folder> b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Mod != b[i].Mod || a[i].Path != b[i].Path || a[i].IsVirtual != b[i].IsVirtual)
                return false;
            if (!a[i].IsVirtual)
                continue;
            if (a[i].Files.Count != b[i].Files.Count)
                return false;
            foreach (var file in a[i].Files)
            {
                if (!b[i].Files.TryGetValue(file.Key, out string real) || real != file.Value)
                    return false;
            }
        }
        return true;
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
