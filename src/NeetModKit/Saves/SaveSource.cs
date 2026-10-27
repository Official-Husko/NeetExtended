using System;
using System.IO;
using NeetLoader.Runtime;

namespace NeetModKit.Saves;

/// <summary>
/// Makes the game's next LoadAll read a <see cref="SaveSnapshot"/> instead of the newest save file:
/// the save text, the mod data and the paint pictures all come from memory, and the player's own
/// saves are never listed or read. It applies to one load; the LoadAll finalizer clears it.
/// </summary>
public static class SaveSource
{
    private const string HandoffKey = "NeetModKit.SaveSource";
    private const string MarkerName = "neet-join.marker";

    public static SaveSnapshot Current { get; private set; }

    public static bool Active => Current != null;

    /// <summary>Raised when LoadAll ends while a snapshot was in use: null if it loaded, else what went wrong.</summary>
    public static event Action<Exception> Loaded;

    /// <summary>The next LoadAll reads <paramref name="snapshot"/> instead of disk.</summary>
    public static void Use(SaveSnapshot snapshot) =>
        Current = snapshot ?? throw new ArgumentNullException(nameof(snapshot));

    public static void Clear() => Current = null;

    /// <summary>
    /// What LoadAll lists as the save folder while a snapshot is in use. LoadAll only looks at the
    /// name (it has to start with "neet") and the write time, so this is a small real file kept in the
    /// framework's folder, not in the save folder.
    /// </summary>
    internal static FileInfo Marker()
    {
        string path = KitMod.Instance.PathTo(MarkerName);
        try
        {
            if (!File.Exists(path))
                File.WriteAllText(path, "Stands in for a save file while a co-op house loads from memory.");
        }
        catch (IOException e)
        {
            KitMod.Instance.Log.Warn("Couldn't create " + MarkerName + ": " + e.Message);
        }
        return new FileInfo(path);
    }

    internal static FileInfo[] GetFiles(DirectoryInfo folder) =>
        Active ? new[] { Marker() } : folder.GetFiles();

    // LoadAll asks for "<id>.png" files; the snapshot answers by id.
    internal static bool PaintExists(string path) =>
        Active ? TryId(path, out int id) && Current.Paint.ContainsKey(id) : File.Exists(path);

    internal static byte[] PaintBytes(string path) =>
        Active && TryId(path, out int id) ? Current.Paint[id] : File.ReadAllBytes(path);

    private static bool TryId(string path, out int id)
    {
        int start = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/')) + 1;
        int end = path.LastIndexOf('.');
        id = 0;
        return end > start && int.TryParse(path.Substring(start, end - start), out id);
    }

    /// <summary>From the LoadAll finalizer.</summary>
    internal static void Finish(Exception error)
    {
        if (!Active)
            return;
        Current = null;
        try { Loaded?.Invoke(error); }
        catch (Exception e) { KitMod.Instance?.Log.Error("A save source handler threw", e); }
    }

    // A framework reload while a house is about to load keeps the snapshot (as encoded bytes).
    internal static void KeepForReload()
    {
        if (Active)
            ReloadState.Put(HandoffKey, Current.Encode());
    }

    internal static void RestoreAfterReload()
    {
        if (ReloadState.Take(HandoffKey, out byte[] data))
            Current = SaveSnapshot.Decode(data);
    }
}
