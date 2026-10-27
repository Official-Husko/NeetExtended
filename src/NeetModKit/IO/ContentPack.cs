using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeetLoader;
using NeetModKit.Conversations;
using NeetModKit.Registry;
using Newtonsoft.Json.Linq;

namespace NeetModKit.IO;

/// <summary>
/// Content files from a folder of every loaded mod ("phone", "email", ...): .ink scripts go into an
/// Ink story, .json files are handed to the mod that reads them. Everything a load registers lives
/// in <see cref="ContentFile.Scope"/> and goes away when the files load again, which happens when a
/// file changes or a mod comes or goes.
/// <code>
/// _pack = ContentPack.Watch(this, "phone", InkStory.Dialogue, file => CallParser.Load(file), () => Log.Info("loaded"));
/// public override void OnAllLoaded() => _pack.Reload();
/// </code>
/// </summary>
public sealed class ContentPack
{
    private static readonly JsonLoadSettings JsonSettings = new JsonLoadSettings { CommentHandling = CommentHandling.Ignore };

    private readonly NeetMod _owner;
    private readonly InkStory _story;
    private readonly Action<ContentFile> _loadJson;
    private readonly Action _loaded;
    private readonly List<string> _problems = new List<string>();
    private readonly List<InkScript> _scripts = new List<InkScript>();
    private ModContent _content;
    private Scope _scope;

    private ContentPack(NeetMod owner, InkStory story, Action<ContentFile> loadJson, Action loaded)
    {
        _owner = owner;
        _story = story;
        _loadJson = loadJson;
        _loaded = loaded;
    }

    /// <summary>The folder name inside each mod.</summary>
    public string FolderName => _content.FolderName;

    /// <summary>Files read in the last load.</summary>
    public int FileCount { get; private set; }

    /// <param name="story">Where .ink files go.</param>
    /// <param name="loadJson">Reads one .json file. Called after every .ink file is in, so JSON can point at its knots.</param>
    /// <param name="loaded">Called after each load, for logging and refreshing.</param>
    public static ContentPack Watch(NeetMod owner, string folderName, InkStory story, Action<ContentFile> loadJson, Action loaded = null)
    {
        var pack = new ContentPack(owner, story, loadJson, loaded);
        pack._content = ModContent.Watch(owner, folderName, pack.LoadAll, "*.json", "*.ink");
        // Files compile conditions from other mods; when those come or go, load again.
        Action changed = pack._content.MarkDirty;
        Rules.RuleRegistry.Changed += changed;
        owner.Track(NeetLoader.Runtime.Disposable.Create(() => Rules.RuleRegistry.Changed -= changed));
        return pack;
    }

    /// <summary>Loads every file again, now.</summary>
    public void Reload() => _content.Load();

    /// <summary>File problems from the last load plus Ink compile problems, each with the file it's in.</summary>
    public List<string> Problems()
    {
        var all = new List<string>(_problems);
        foreach (var script in _scripts)
        {
            foreach (string problem in script.Problems)
                all.Add($"{script.Name}: {problem}");
        }
        return all;
    }

    /// <summary>Logs a one-line summary and every problem.</summary>
    public void LogSummary(string what)
    {
        var problems = Problems();
        _owner.Log.Info($"Loaded {what} from {FileCount} files" + (problems.Count > 0 ? $", {problems.Count} problem(s):" : "."));
        foreach (string problem in problems)
            _owner.Log.Warn(problem);
    }

    private void LoadAll()
    {
        _scope?.Dispose();
        _scope = new Scope(_owner);
        _problems.Clear();
        _scripts.Clear();
        FileCount = 0;

        var jsonFiles = new List<ContentFile>();
        foreach (var folder in _content.Folders())
        {
            var files = Directory.GetFiles(folder.Path, "*.*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            foreach (string file in files.Where(f => f.EndsWith(".ink", StringComparison.OrdinalIgnoreCase)))
            {
                FileCount++;
                try { _scripts.Add(InkScripts.Add(_scope, _story, folder.Display(file), File.ReadAllText(file))); }
                catch (IOException e) { _problems.Add($"{folder.Display(file)}: {e.Message}"); }
            }
            foreach (string file in files.Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                jsonFiles.Add(new ContentFile(this, file, folder.Display(file), folder.Mod.Id));
        }

        // Scripts from every mod first, so a file can use another mod's knots.
        foreach (var file in jsonFiles)
        {
            FileCount++;
            try { _loadJson(file); }
            catch (Exception e) { file.Problem(e.Message); }
        }
        _loaded?.Invoke();
    }

    internal Scope Scope => _scope;

    internal InkStory Story => _story;

    internal void AddProblem(string problem) => _problems.Add(problem);

    internal void AddScript(InkScript script) => _scripts.Add(script);

    internal static JToken Parse(string path) => JToken.Parse(File.ReadAllText(path), JsonSettings);
}

/// <summary>One .json file of a <see cref="ContentPack"/>, while it loads.</summary>
public sealed class ContentFile
{
    private readonly ContentPack _pack;

    internal ContentFile(ContentPack pack, string path, string display, string modId)
    {
        _pack = pack;
        Path = path;
        Display = display;
        ModId = modId;
    }

    public string Path { get; }

    /// <summary>"ModId/folder/file.json", for messages.</summary>
    public string Display { get; }

    /// <summary>The mod whose folder it's in. Ids in the file are usually "ModId/id".</summary>
    public string ModId { get; }

    /// <summary>Register what the file adds here: it's removed before the next load.</summary>
    public NeetLoader.Runtime.ITracker Scope => _pack.Scope;

    /// <summary>The file as JSON (comments allowed). Throws on bad JSON; the pack reports it.</summary>
    public JToken Read() => ContentPack.Parse(Path);

    /// <summary>
    /// The objects in the file: one object, a list of them, or <c>{ "listKey": [ ... ] }</c>.
    /// Reports a problem when there are none.
    /// </summary>
    public List<JObject> Items(string listKey)
    {
        var root = Read();
        var list = new List<JObject>();
        var items = root is JObject single && single[listKey] is JArray wrapped ? wrapped : root as JArray;
        if (items == null && root is JObject one)
            list.Add(one);
        else if (items != null)
        {
            foreach (var item in items)
            {
                if (item is JObject obj)
                    list.Add(obj);
            }
        }
        if (list.Count == 0)
            Problem($"nothing in this file (expected an object, a list, or {{ \"{listKey}\": [...] }}).");
        return list;
    }

    /// <summary>How to name item <paramref name="index"/> of <paramref name="count"/> in messages: by its id, or its number.</summary>
    public static string Where(string kind, JObject item, int index, int count) =>
        item["id"] != null ? $"{kind} \"{item["id"]}\"" : count > 1 ? $"{kind} {index + 1}" : kind;

    /// <summary>Reports a problem in this file, shown on the mod's page and in the log.</summary>
    public void Problem(string message) => _pack.AddProblem($"{Display}: {message}");

    /// <summary>Compiles generated Ink into the pack's story, under this file's name.</summary>
    public InkScript AddInk(string source)
    {
        var script = InkScripts.Add(_pack.Scope, _pack.Story, Display, source);
        _pack.AddScript(script);
        return script;
    }
}
