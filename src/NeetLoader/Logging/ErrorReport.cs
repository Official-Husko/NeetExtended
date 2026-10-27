using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NeetLoader.Mods;
using NeetLoader.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeetLoader.Logging;

/// <summary>
/// errors.log: a short report of what went wrong this session, meant to be attached to a bug report.
/// Every error logged by the game or a mod is in it once, with how often it happened, plus problems
/// mods report on purpose (<see cref="Add(string,string,string)"/>), under a header with the game,
/// loader and mod versions. The previous session's report is kept as errors-previous.log. No errors,
/// no file. The mod menu's Errors tab shows the same list.
/// </summary>
public static class ErrorReport
{
    private const int MaxEntries = 200;
    private const int MaxDetailLines = 40;

    /// <summary>One problem in the report. The count and last time go up while the game runs.</summary>
    public sealed class Problem
    {
        public string Source { get; internal set; }
        public string Title { get; internal set; }
        public string Details { get; internal set; }
        public string Scene { get; internal set; }
        public DateTime First { get; internal set; }
        public DateTime Last { get; internal set; }
        public int Count { get; internal set; }

        /// <summary>True when a mod caught and reported it; false for errors that were logged (crashes, exceptions).</summary>
        public bool Caught { get; internal set; }
    }

    private static readonly object Lock = new object();
    private static readonly List<Problem> Entries = new List<Problem>();
    private static readonly Dictionary<string, Problem> ByKey = new Dictionary<string, Problem>();
    private static int _notListed;
    private static bool _dirty;
    private static string _path;

    /// <summary>Where the report is written.</summary>
    public static string FilePath => _path;

    /// <summary>How many different problems were reported this session.</summary>
    public static int Count
    {
        get
        {
            lock (Lock)
                return Entries.Count + _notListed;
        }
    }

    /// <summary>Goes up whenever a new problem is added or the report is cleared (not when a count goes up).</summary>
    public static int Version { get; private set; }

    /// <summary>The problems so far, oldest first. The list is a copy; the problems are live.</summary>
    public static List<Problem> Snapshot()
    {
        lock (Lock)
            return new List<Problem>(Entries);
    }

    /// <summary>Empties the report (and errors.log), for example after it was sent.</summary>
    public static void Clear()
    {
        lock (Lock)
        {
            Entries.Clear();
            ByKey.Clear();
            _notListed = 0;
            _dirty = false;
            Version++;
        }
        try
        {
            if (_path != null && File.Exists(_path))
                File.Delete(_path);
        }
        catch (IOException) { /* Rewritten on the next problem anyway. */ }
    }

    /// <summary>
    /// Adds a problem a mod caught to the report. <paramref name="title"/> is one readable line ("Story
    /// event SpawnThiefIntro failed"); <paramref name="details"/> is anything that helps find the cause.
    /// The same problem again only raises its count.
    /// </summary>
    public static void Add(string source, string title, string details = null) => Add(source, title, details, caught: true);

    private static void Add(string source, string title, string details, bool caught)
    {
        string scene = SafeSceneName();
        lock (Lock)
        {
            string key = source + "\n" + title + "\n" + FirstLines(details, 3);
            if (ByKey.TryGetValue(key, out var entry))
            {
                entry.Count++;
                entry.Last = DateTime.Now;
                _dirty = true;
                return;
            }
            if (Entries.Count >= MaxEntries)
            {
                _notListed++;
                return;
            }
            entry = new Problem
            {
                Caught = caught,
                Source = source,
                Title = title,
                Details = Trim(details),
                Scene = scene,
                First = DateTime.Now,
                Last = DateTime.Now,
                Count = 1,
            };
            Entries.Add(entry);
            ByKey[key] = entry;
            _dirty = true;
            Version++;
        }
    }

    /// <summary>Same as <see cref="Add(string,string,string)"/> with the exception and its stack added to the details.</summary>
    public static void Add(string source, string title, Exception exception, string details = null) =>
        Add(source, title, details == null ? exception?.ToString() : details + "\n" + exception);

    /// <summary>The report as text, for copying.</summary>
    public static string Text()
    {
        lock (Lock)
            return Render();
    }

    internal static void Init(string logsDir)
    {
        _path = Path.Combine(logsDir, "errors" + Instance.Suffix + ".log");
        string previous = Path.Combine(logsDir, "errors-previous" + Instance.Suffix + ".log");
        try
        {
            if (File.Exists(previous))
                File.Delete(previous);
            if (File.Exists(_path))
                File.Move(_path, previous);
        }
        catch (IOException)
        {
            // Kept open by another program; it's overwritten on the first error instead.
        }
    }

    /// <summary>Called for every error line the log hub sees. The first line is the title.</summary>
    internal static void Capture(string source, string message)
    {
        if (string.IsNullOrEmpty(message))
            return;
        int newline = message.IndexOf('\n');
        Add(source, newline < 0 ? message : message.Substring(0, newline), newline < 0 ? null : message.Substring(newline + 1), caught: false);
    }

    /// <summary>Rewrites the file if anything changed. Called about once a second and when the game closes.</summary>
    internal static void Flush()
    {
        string text;
        lock (Lock)
        {
            if (!_dirty || _path == null)
                return;
            _dirty = false;
            text = Render();
        }
        try { File.WriteAllText(_path, text, new UTF8Encoding(false)); }
        catch (IOException) { /* Try again on the next flush. */ }
    }

    private static string Render()
    {
        var sb = new StringBuilder();
        sb.Append("NeetExtended error report\n");
        sb.Append("Written ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
        sb.Append("Game ").Append(Application.productName).Append(' ').Append(Application.version)
          .Append(", Unity ").Append(Application.unityVersion)
          .Append(", NeetLoader ").Append(Loader.Version)
          .Append(", ").Append(SystemInfo.operatingSystem).Append('\n');
        sb.Append("Mods:");
        foreach (var mod in ModRegistry.Mods)
            sb.Append(' ').Append(mod.Id).Append(' ').Append(mod.Manifest.Version).Append(mod.State == ModState.Loaded ? "" : " (" + mod.State + ")").Append(',');
        if (sb[sb.Length - 1] == ',')
            sb.Length--;
        sb.Append("\n\n");

        sb.Append(Entries.Count + _notListed).Append(Entries.Count + _notListed == 1 ? " problem" : " problems").Append(" this session.\n");
        foreach (var entry in Entries)
        {
            sb.Append("\n[").Append(entry.First.ToString("HH:mm:ss")).Append("] ").Append(entry.Caught ? "CAUGHT " : "ERROR ")
              .Append(entry.Source).Append(": ").Append(entry.Title).Append('\n');
            sb.Append("  ").Append(entry.Count == 1 ? "Once" : entry.Count + " times");
            if (entry.Count > 1)
                sb.Append(", last at ").Append(entry.Last.ToString("HH:mm:ss"));
            if (!string.IsNullOrEmpty(entry.Scene))
                sb.Append(", in ").Append(entry.Scene);
            sb.Append(".\n");
            if (!string.IsNullOrEmpty(entry.Details))
            {
                foreach (string line in entry.Details.Split('\n'))
                    sb.Append("  ").Append(line.TrimEnd()).Append('\n');
            }
        }
        if (_notListed > 0)
            sb.Append("\n...and ").Append(_notListed).Append(" more not listed.\n");
        return sb.ToString();
    }

    private static string Trim(string details)
    {
        if (string.IsNullOrEmpty(details))
            return null;
        var lines = details.TrimEnd().Split('\n');
        if (lines.Length <= MaxDetailLines)
            return string.Join("\n", lines);
        return string.Join("\n", lines, 0, MaxDetailLines) + $"\n(+{lines.Length - MaxDetailLines} more lines)";
    }

    private static string FirstLines(string text, int count)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        int end = -1;
        for (int i = 0; i < count; i++)
        {
            end = text.IndexOf('\n', end + 1);
            if (end < 0)
                return text;
        }
        return text.Substring(0, end);
    }

    // Unity's scene API only works on the main thread; errors can come from others.
    private static string SafeSceneName()
    {
        try { return Runtime.MainThread.IsCurrent ? SceneManager.GetActiveScene().name : null; }
        catch (Exception) { return null; }
    }
}
