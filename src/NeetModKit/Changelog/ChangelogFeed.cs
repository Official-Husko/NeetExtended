using System.Collections.Generic;
using System.IO;
using NeetLoader.Config;
using NeetLoader.Mods;

namespace NeetModKit.Changelog;

/// <summary>A changelog and whose it is.</summary>
public sealed class ChangelogSource
{
    public string Title;
    public ChangelogDocument Document;
}

/// <summary>
/// Collects changelog.json files from every loaded mod folder. NeetModKit's own file covers the
/// whole pack (loader, framework, tool and bundled mods) and is listed first.
/// </summary>
public static class ChangelogFeed
{
    public const string FileName = "changelog.json";
    public const string PackTitle = About.PackInfo.Name;

    public static List<ChangelogSource> Load()
    {
        var sources = new List<ChangelogSource>();
        foreach (var mod in ModRegistry.Mods)
        {
            if (mod.State != ModState.Loaded)
                continue;
            string path = Path.Combine(mod.Directory, FileName);
            if (!File.Exists(path) || !JsonFile.TryRead(path, out ChangelogDocument doc, out _))
                continue;

            bool isPack = mod.Id == KitMod.Instance?.Info.Id;
            var source = new ChangelogSource { Title = isPack ? PackTitle : mod.Manifest.DisplayName, Document = doc };
            if (isPack)
                sources.Insert(0, source);
            else
                sources.Add(source);
        }
        return sources;
    }

    /// <summary>Newest version in the pack changelog, used to decide whether to show What's New.</summary>
    public static string PackVersion()
    {
        var sources = Load();
        return sources.Count > 0 && sources[0].Title == PackTitle ? sources[0].Document.NewestVersion : null;
    }
}
