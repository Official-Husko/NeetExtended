using System.Collections.Generic;
using EmailExpansion.Mail;
using NeetModKit.Conversations;
using NeetModKit.IO;

namespace EmailExpansion.Packs;

/// <summary>
/// Loads email files from the "email" folder of every loaded mod (this one included): .ink scripts
/// into the game's email story and .json email files. Everything loads again when a file changes or
/// a mod comes or goes, so a mod made only of a mod.json and an email folder is a complete pack.
/// </summary>
internal static class MailPacks
{
    public const string FolderName = "email";

    private static ContentPack _pack;

    public static int FileCount => _pack?.FileCount ?? 0;

    public static List<string> Problems() => _pack?.Problems() ?? new List<string>();

    public static void Init(EmailExpansionMod mod) =>
        _pack = ContentPack.Watch(mod, FolderName, InkStory.Email, MailParser.LoadFile,
            () => _pack.LogSummary($"{MailBook.All.Count} emails"));

    public static void Reload() => _pack.Reload();
}
