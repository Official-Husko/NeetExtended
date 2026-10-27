using System.Collections.Generic;
using System.IO;
using NeetLoader.Mods;
using NeetModKit.IO;

namespace Coop.Assets.Media;

/// <summary>
/// This game's media folders (every loaded mod's "tv" and "mixtapes"), with every file's id, for
/// when it hosts: the players get the same TV stations and tapes. Files are hashed in the background
/// as soon as the mod loads and whenever a folder changes (unchanged files come from the hash index),
/// so the list is ready by the time someone joins.
/// </summary>
internal static class MediaHost
{
    /// <summary>The media folders whose contents shape the shared house (Custom TV Stations, Mixtapes).</summary>
    public static readonly string[] FolderNames = { "tv", "mixtapes" };

    private static List<MediaFolderInfo> _folders = new List<MediaFolderInfo>();
    private static List<MediaFolderInfo> _building;
    private static int _hashing;
    private static bool _changed = true;
    private static int _version;

    /// <summary>Raised when a new list is ready (after a change).</summary>
    public static event System.Action Ready;

    public static bool IsReady => _building == null && !_changed;

    /// <summary>
    /// The folders as far as they're known: while hashing, files not done yet have no id (a joiner
    /// still gets every tape and station by name, and the files with the next list).
    /// </summary>
    public static List<MediaFolderInfo> Current => _building ?? _folders;

    public static void Init(CoopMod mod)
    {
        FileWatch.Start(mod, WatchedFolders, () => _changed = true, 2f, "*");
        ModRegistry.ModLoaded += MarkChanged;
        ModRegistry.ModUnloading += MarkChanged;
        mod.Track(NeetLoader.Runtime.Disposable.Create(() =>
        {
            ModRegistry.ModLoaded -= MarkChanged;
            ModRegistry.ModUnloading -= MarkChanged;
        }));
    }

    public static void Tick()
    {
        if (_changed && _building == null)
            Build();
    }

    private static void MarkChanged(ModInfo mod) => _changed = true;

    private static IEnumerable<string> WatchedFolders()
    {
        foreach (var mod in ModRegistry.Mods)
        {
            if (mod.State != ModState.Loaded)
                continue;
            foreach (string name in FolderNames)
                yield return Path.Combine(mod.Directory, name);
        }
    }

    private static void Build()
    {
        _changed = false;
        _building = new List<MediaFolderInfo>();
        int version = ++_version;
        _hashing = 1; // Held until every file is queued.
        foreach (var mod in ModRegistry.Mods)
        {
            if (mod.State != ModState.Loaded)
                continue;
            foreach (string name in FolderNames)
            {
                string path = Path.Combine(mod.Directory, name);
                if (Directory.Exists(path))
                    _building.Add(Read(mod, name, path, version));
            }
        }
        Done(version);
    }

    private static MediaFolderInfo Read(ModInfo mod, string name, string path, int version)
    {
        var folder = new MediaFolderInfo { FolderName = name, ModId = mod.Id };
        string[] files;
        try
        {
            files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
        }
        catch (IOException)
        {
            return folder;
        }
        foreach (string file in files)
        {
            string relative = file.Substring(path.Length).TrimStart('/', '\\').Replace('\\', '/');
            string ext = FileTypes.ExtOf(file);
            if (ext == null || !MediaPaths.IsSafe(relative))
                continue;
            if (!FileTypes.IsMedia(ext))
            {
                AddText(folder, file, relative);
                continue;
            }
            int index = folder.Files.Count;
            folder.Files.Add(new KeyValuePair<string, AssetId>(relative, default));
            _hashing++;
            FileHasher.Hash(file, id =>
            {
                if (version != _version)
                    return;
                if (id.IsValid)
                    LocalFiles.Add(id, file);
                folder.Files[index] = new KeyValuePair<string, AssetId>(relative, id);
                Done(version);
            });
        }
        return folder;
    }

    // List files (stations.jsonc, tapes.jsonc) at the top of the folder travel as text.
    private static void AddText(MediaFolderInfo folder, string file, string relative)
    {
        if (relative.Contains("/"))
            return;
        try
        {
            if (new FileInfo(file).Length <= MediaListing.MaxText)
                folder.Texts.Add(new KeyValuePair<string, string>(relative, File.ReadAllText(file)));
            else
                CoopMod.Log.Warn($"Files: {relative} in {folder.ModId}/{folder.FolderName} is too big to send; the players get the folder without it.");
        }
        catch (IOException)
        {
        }
    }

    private static void Done(int version)
    {
        if (version != _version || --_hashing > 0)
            return;
        _folders = _building;
        _building = null;
        int files = 0;
        foreach (var folder in _folders)
            files += folder.Files.Count;
        CoopMod.Log.Debug($"Files: {files} media file(s) in {_folders.Count} folder(s) ready to share.");
        if (!_changed)
            Ready?.Invoke();
    }
}
