using System.Collections.Generic;
using System.Text;
using NeetLoader.Logging;

namespace NeetModKit.Menu.Pages;

/// <summary>
/// The last few log lines of one mod as rich text. Rebuilt only when something new was logged,
/// so calling it from Refresh costs next to nothing.
/// </summary>
internal sealed class ModLogView
{
    private const int Lines = 14;
    private const int ScanBack = 1500;

    private readonly StringBuilder _text = new StringBuilder();
    private readonly List<string> _lines = new List<string>(Lines);
    private string _source;
    private long _seen = -1;

    public bool TryGet(string source, out string text)
    {
        long last = LogHub.Buffer.LastSequence;
        if (source == _source && last == _seen)
        {
            text = null;
            return false;
        }
        _source = source;
        _seen = last;

        _lines.Clear();
        var recent = LogHub.Buffer.Read(last - ScanBack, ScanBack);
        for (int i = recent.Count - 1; i >= 0 && _lines.Count < Lines; i--)
        {
            var e = recent[i];
            if (e.Source != source)
                continue;
            var color = e.Level >= LogLevel.Error ? MenuPalette.Bad : e.Level == LogLevel.Warning ? MenuPalette.Warn : MenuPalette.Muted;
            _lines.Add(MenuPalette.Paint($"{e.Time:HH:mm:ss}", MenuPalette.Muted) + "  " + MenuPalette.Paint(FirstLine(e.Message), color));
        }

        _text.Clear();
        for (int i = _lines.Count - 1; i >= 0; i--)
            _text.AppendLine(_lines[i]);
        text = _lines.Count == 0 ? MenuPalette.Paint("Nothing logged yet.", MenuPalette.Muted) : _text.ToString();
        return true;
    }

    private static string FirstLine(string message)
    {
        int nl = message.IndexOf('\n');
        return nl < 0 ? message : message.Substring(0, nl) + " …";
    }
}
