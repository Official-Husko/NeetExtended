using System;
using System.Collections.Generic;
using System.IO;
using NeetLoader.Runtime;
using UnityEngine;

namespace NeetModKit.IO;

/// <summary>
/// Calls back when files in a set of folders are added, removed or changed. Checks once a second
/// (polling is reliable under Proton, file system events are not) and only compares names, sizes
/// and write times, so it's cheap for the small data folders mods keep.
/// </summary>
public sealed class FileWatch
{
    private static readonly List<FileWatch> Active = new List<FileWatch>();

    private readonly Func<IEnumerable<string>> _folders;
    private readonly string[] _patterns;
    private readonly Action _changed;
    private readonly float _interval;
    private long _signature;
    private float _nextCheck;

    private FileWatch(Func<IEnumerable<string>> folders, string[] patterns, Action changed, float interval)
    {
        _folders = folders;
        _patterns = patterns;
        _changed = changed;
        _interval = interval;
        _signature = Compute();
    }

    /// <summary>
    /// Watches the folders <paramref name="folders"/> returns (asked again on every check, so the
    /// list can change) for files matching <paramref name="patterns"/>, searched recursively.
    /// </summary>
    public static void Start(ITracker owner, Func<IEnumerable<string>> folders, Action changed, float interval = 1f, params string[] patterns)
    {
        var watch = new FileWatch(folders, patterns.Length > 0 ? patterns : new[] { "*" }, changed, interval);
        Active.Add(watch);
        owner.Track(Disposable.Create(() => Active.Remove(watch)));
    }

    /// <summary>Driven by the framework once per frame.</summary>
    internal static void TickAll()
    {
        if (Active.Count == 0)
            return;
        float now = Time.realtimeSinceStartup;
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (i < Active.Count && now >= Active[i]._nextCheck)
                Active[i].Check(now);
        }
    }

    private void Check(float now)
    {
        _nextCheck = now + _interval;
        long signature = Compute();
        if (signature == _signature)
            return;
        _signature = signature;
        try { _changed(); }
        catch (Exception e) { KitMod.Instance?.Log.Error("A file watch handler threw", e); }
    }

    private long Compute()
    {
        unchecked
        {
            long hash = 17;
            foreach (string folder in _folders())
            {
                if (!Directory.Exists(folder))
                    continue;
                foreach (string pattern in _patterns)
                {
                    foreach (string file in Directory.GetFiles(folder, pattern, SearchOption.AllDirectories))
                    {
                        var info = new FileInfo(file);
                        hash = hash * 31 + file.GetHashCode();
                        hash = hash * 31 + info.Length;
                        hash = hash * 31 + info.LastWriteTimeUtc.Ticks;
                    }
                }
            }
            return hash;
        }
    }
}
