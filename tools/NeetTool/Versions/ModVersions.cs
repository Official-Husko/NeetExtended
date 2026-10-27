using System.Text.Json.Nodes;
using NeetTool.Changelog;

namespace NeetTool.Versions;

/// <summary>
/// Each mod's version against the last release: a mod whose files changed since the release tag
/// needs a new version in its mod.json, and every dependency's minimum has to exist.
/// </summary>
public static class ModVersions
{
    public enum State
    {
        Unchanged,
        New,
        Bumped,
        NeedsBump,
    }

    public sealed record Mod(string Id, string Folder, string Version, string? Released, int ChangedFiles, State State, Dictionary<string, string> Dependencies);

    /// <summary>Every mod in the repo (the framework and mods/*), compared with <paramref name="tag"/> (null before the first release).</summary>
    public static List<Mod> Read(string repo, string? tag)
    {
        var mods = new List<Mod>();
        foreach (string folder in Folders(repo))
        {
            string relative = Path.GetRelativePath(repo, folder).Replace('\\', '/');
            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "mod.json")))!;
            string id = json["id"]?.GetValue<string>() ?? Path.GetFileName(folder);
            string version = json["version"]?.GetValue<string>() ?? "0.0.0";
            var dependencies = new Dictionary<string, string>();
            if (json["dependencies"] is JsonObject deps)
            {
                foreach (var pair in deps)
                    dependencies[pair.Key] = pair.Value?.GetValue<string>() ?? "*";
            }

            string? released = tag == null ? null : ReleasedVersion(repo, tag, relative);
            int changed = tag == null ? 0 : ChangedFiles(repo, tag, relative);
            var state = released == null ? State.New
                : released != version ? State.Bumped
                : changed > 0 ? State.NeedsBump
                : State.Unchanged;
            mods.Add(new Mod(id, relative, version, released, changed, state, dependencies));
        }
        var loader = Loader(repo, tag);
        if (loader != null)
            mods.Insert(0, loader);
        return mods;
    }

    // The loader isn't a mod; its version is Loader.Version in src/NeetLoader/Loader.cs.
    private const string LoaderFolder = "src/NeetLoader";
    private const string LoaderFile = LoaderFolder + "/Loader.cs";

    private static Mod? Loader(string repo, string? tag)
    {
        string path = Path.Combine(repo, LoaderFile);
        if (!File.Exists(path))
            return null;
        string version = LoaderVersion(File.ReadAllText(path)) ?? "0.0.0";
        string? released = null;
        if (tag != null)
        {
            try { released = LoaderVersion(Git.Run(repo, "show", $"{tag}:{LoaderFile}")); }
            catch (InvalidOperationException) { }
        }
        int changed = tag == null ? 0 : ChangedFiles(repo, tag, LoaderFolder);
        var state = released == null ? State.New
            : released != version ? State.Bumped
            : changed > 0 ? State.NeedsBump
            : State.Unchanged;
        return new Mod("NeetLoader", LoaderFolder, version, released, changed, state, new Dictionary<string, string>());
    }

    private static string? LoaderVersion(string source)
    {
        var match = System.Text.RegularExpressions.Regex.Match(source, @"const string Version = ""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>Dependencies asking for more than the repo has: "Needs NeetModKit 0.3.0, it's at 0.2.0".</summary>
    public static List<string> DependencyProblems(List<Mod> mods)
    {
        var byId = mods.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        foreach (var mod in mods)
        {
            foreach (var (id, minimum) in mod.Dependencies)
            {
                if (minimum == "*" || !byId.TryGetValue(id, out var dependency))
                    continue;
                if (Compare(dependency.Version, minimum) < 0)
                    problems.Add($"{mod.Id} needs {id} {minimum}, but {id} is at {dependency.Version}");
            }
        }
        return problems;
    }

    private static IEnumerable<string> Folders(string repo)
    {
        string kit = Path.Combine(repo, "src", "NeetModKit");
        if (File.Exists(Path.Combine(kit, "mod.json")))
            yield return kit;
        foreach (string folder in Directory.GetDirectories(Path.Combine(repo, "mods")).OrderBy(f => f, StringComparer.Ordinal))
        {
            if (File.Exists(Path.Combine(folder, "mod.json")))
                yield return folder;
        }
    }

    private static string? ReleasedVersion(string repo, string tag, string folder)
    {
        try
        {
            var json = JsonNode.Parse(Git.Run(repo, "show", $"{tag}:{folder}/mod.json"));
            return json?["version"]?.GetValue<string>() ?? "0.0.0";
        }
        catch (InvalidOperationException)
        {
            return null; // Not in that release.
        }
    }

    // Against the working tree, so uncommitted changes count too.
    private static int ChangedFiles(string repo, string tag, string folder) =>
        Git.Run(repo, "diff", "--name-only", tag, "--", folder)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Compares loose SemVer like the loader does: missing parts are 0, pre-release tags are ignored.</summary>
    public static int Compare(string a, string b)
    {
        int[] x = Parts(a), y = Parts(b);
        for (int i = 0; i < 3; i++)
        {
            if (x[i] != y[i])
                return x[i].CompareTo(y[i]);
        }
        return 0;
    }

    private static int[] Parts(string version)
    {
        string core = version.TrimStart('>', '=', ' ').Split('-', '+')[0];
        var parts = new int[3];
        string[] split = core.Split('.');
        for (int i = 0; i < 3 && i < split.Length; i++)
            int.TryParse(split[i], out parts[i]);
        return parts;
    }
}
