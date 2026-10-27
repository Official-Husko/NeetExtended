using System.Collections.Generic;
using NeetModKit.Conversations;
using NeetModKit.IO;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Packs;

/// <summary>
/// Loads call files from the "phone" folder of every loaded mod (this one included): .ink scripts
/// and .json call files. Everything is loaded again when a file changes or a mod comes or goes.
/// A mod made only of a mod.json and a phone folder is a complete call pack.
/// </summary>
internal static class PackLoader
{
    public const string FolderName = "phone";

    private static ContentPack _pack;

    public static int FileCount => _pack?.FileCount ?? 0;

    /// <summary>File errors plus any compile errors of the generated or hand-written Ink.</summary>
    public static List<string> Problems() => _pack?.Problems() ?? new List<string>();

    public static void Init(PhoneExpansionMod mod) =>
        _pack = ContentPack.Watch(mod, FolderName, InkStory.Dialogue, CallParser.LoadFile,
            () => _pack.LogSummary($"{PhoneBook.All.Count} calls"));

    /// <summary>Loads every call file again, now.</summary>
    public static void Reload() => _pack.Reload();
}
