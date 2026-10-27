using System.Collections.Generic;
using System.IO;
using NeetLoader.Config;
using NeetLoader.Logging;

namespace NeetLoader.Mods;

/// <summary>Finds mod folders under &lt;game&gt;/Mods. A mod folder is any folder with a mod.json.</summary>
internal static class ModDiscovery
{
    public const string ManifestFile = "mod.json";

    public static List<ModInfo> Scan(string modsDir, ModLogger log)
    {
        var mods = new List<ModInfo>();
        var seen = new HashSet<string>();

        foreach (string dir in Directory.GetDirectories(modsDir))
        {
            string manifestPath = Path.Combine(dir, ManifestFile);
            if (!File.Exists(manifestPath))
                continue;

            if (!JsonFile.TryRead(manifestPath, out ModManifest manifest, out var error))
            {
                log.Error($"Skipping {Path.GetFileName(dir)}: bad mod.json ({error?.Message})");
                continue;
            }

            if (string.IsNullOrEmpty(manifest.Id))
                manifest.Id = Path.GetFileName(dir);

            if (!seen.Add(manifest.Id))
            {
                log.Error($"Skipping {dir}: another mod already uses the id '{manifest.Id}'");
                continue;
            }

            mods.Add(new ModInfo(manifest, dir));
        }

        return mods;
    }

    /// <summary>Resolves the DLL list for a mod (explicit list or every DLL in the folder root).</summary>
    public static IEnumerable<string> AssemblyPaths(ModInfo mod)
    {
        if (mod.Manifest.Assemblies.Count == 0)
            return Directory.GetFiles(mod.Directory, "*.dll", SearchOption.TopDirectoryOnly);

        var paths = new List<string>(mod.Manifest.Assemblies.Count);
        foreach (string relative in mod.Manifest.Assemblies)
            paths.Add(Path.Combine(mod.Directory, relative));
        return paths;
    }

    /// <summary>
    /// DLLs in the folder root that aren't in an explicit assembly list: libraries the mod uses. They
    /// load on first use and are never reloaded, so objects made from them outlive a hot reload.
    /// Then the DLLs in the x86/ or x64/ subfolder, whichever matches the game's player: builds of a
    /// library for one bitness. They come last, so they stand in for a root DLL of the same name.
    /// </summary>
    public static IEnumerable<string> LibraryPaths(ModInfo mod)
    {
        if (mod.Manifest.Assemblies.Count > 0)
        {
            var listed = new HashSet<string>(AssemblyPaths(mod), System.StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(mod.Directory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                if (!listed.Contains(Path.GetFullPath(path)) && !listed.Contains(path))
                    yield return path;
            }
        }

        string bitness = Path.Combine(mod.Directory, System.IntPtr.Size == 8 ? "x64" : "x86");
        if (!Directory.Exists(bitness))
            yield break;
        foreach (string path in Directory.GetFiles(bitness, "*.dll", SearchOption.TopDirectoryOnly))
            yield return path;
    }
}
