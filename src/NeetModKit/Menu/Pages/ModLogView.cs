using System.Collections.Generic;
using NeetLoader.Logging;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>
/// The last few log lines of one mod, refreshed at most twice a second and only when something
/// new was logged, so drawing it every frame costs nothing.
/// </summary>
internal sealed class ModLogView
{
    private const int Lines = 14;
    private const int ScanBack = 1500;
    private const float RefreshInterval = 0.5f;

    private readonly List<string> _lines = new List<string>(Lines);
    private string _source;
    private long _seenSequence = -1;
    private float _nextRefresh;

    public IReadOnlyList<string> Get(string source)
    {
        if (source != _source)
        {
            _source = source;
            _seenSequence = -1;
            _nextRefresh = 0f;
        }

        long last = LogHub.Buffer.LastSequence;
        if (last != _seenSequence && Time.unscaledTime >= _nextRefresh)
        {
            _seenSequence = last;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            Rebuild(last);
        }
        return _lines;
    }

    private void Rebuild(long last)
    {
        _lines.Clear();
        var recent = LogHub.Buffer.Read(last - ScanBack, ScanBack);
        for (int i = recent.Count - 1; i >= 0 && _lines.Count < Lines; i--)
        {
            var e = recent[i];
            if (e.Source != _source)
                continue;
            var color = e.Level >= LogLevel.Error ? MenuPalette.Bad : e.Level == LogLevel.Warning ? MenuPalette.Warn : MenuPalette.Muted;
            _lines.Add(MenuPalette.Paint($"{e.Time:HH:mm:ss}", MenuPalette.Edge) + "  " + MenuPalette.Paint(FirstLine(e.Message), color));
        }
        _lines.Reverse();
    }

    private static string FirstLine(string message)
    {
        int nl = message.IndexOf('\n');
        return nl < 0 ? message : message.Substring(0, nl) + " …";
    }
}
