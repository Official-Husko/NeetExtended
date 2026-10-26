using System.Text.RegularExpressions;

namespace NeetTool.Game;

/// <summary>Finds Steam folders and per-app Proton settings by reading Steam's VDF files.</summary>
public static partial class Steam
{
    public const int AppId = 2054460;

    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string? Root => new[]
    {
        Path.Combine(Home, ".local/share/Steam"),
        Path.Combine(Home, ".steam/steam"),
        Path.Combine(Home, ".var/app/com.valvesoftware.Steam/.local/share/Steam"),
        @"C:\Program Files (x86)\Steam",
    }.FirstOrDefault(Directory.Exists);

    /// <summary>Every steamapps folder Steam knows about (all library drives).</summary>
    public static IEnumerable<string> LibraryFolders()
    {
        if (Root is null)
            yield break;

        string main = Path.Combine(Root, "steamapps");
        yield return main;

        string vdf = Path.Combine(main, "libraryfolders.vdf");
        if (!File.Exists(vdf))
            yield break;

        foreach (Match m in PathEntry().Matches(File.ReadAllText(vdf)))
        {
            string apps = Path.Combine(m.Groups[1].Value.Replace(@"\\", @"\"), "steamapps");
            if (!string.Equals(Path.GetFullPath(apps), Path.GetFullPath(main), StringComparison.Ordinal))
                yield return apps;
        }
    }

    public static string? CompatDataDir()
    {
        foreach (string lib in LibraryFolders())
        {
            string dir = Path.Combine(lib, "compatdata", AppId.ToString());
            if (Directory.Exists(dir))
                return dir;
        }
        return null;
    }

    /// <summary>Name of the compatibility tool picked for the game in Steam (e.g. "GE-Proton10-34").</summary>
    public static string? CompatToolName()
    {
        if (Root is null)
            return null;
        string config = Path.Combine(Root, "config", "config.vdf");
        if (!File.Exists(config))
            return null;
        var m = Regex.Match(File.ReadAllText(config), "\"" + AppId + "\"\\s*\\{\\s*\"name\"\\s*\"([^\"]*)\"");
        return m.Success && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : null;
    }

    /// <summary>Path to the proton script for a tool name, searching custom and official installs.</summary>
    public static string? ProtonScript(string toolName)
    {
        if (Root is null)
            return null;

        var candidates = new List<string> { Path.Combine(Root, "compatibilitytools.d", toolName, "proton") };
        foreach (string lib in LibraryFolders())
            candidates.Add(Path.Combine(lib, "common", toolName, "proton"));

        // Official builds use display names ("Proton 9.0") that don't match the internal id ("proton_9").
        if (toolName.StartsWith("proton_", StringComparison.OrdinalIgnoreCase))
        {
            string version = toolName["proton_".Length..];
            foreach (string lib in LibraryFolders())
                candidates.Add(Path.Combine(lib, "common", $"Proton {version}.0", "proton"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    [GeneratedRegex("\"path\"\\s*\"([^\"]+)\"")]
    private static partial Regex PathEntry();
}
