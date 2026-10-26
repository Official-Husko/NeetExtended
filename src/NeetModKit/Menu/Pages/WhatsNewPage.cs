using System.Collections.Generic;
using NeetModKit.Changelog;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>Release notes from changelog.json, newest first.</summary>
internal sealed class WhatsNewPage : MenuPage
{
    public const string PageTitle = "What's New";

    /// <summary>A release turned into ready-to-draw rich-text strings.</summary>
    private sealed class PreparedRelease
    {
        public string Heading;
        public readonly List<(string Title, Color Color, string[] Lines)> Sections = new List<(string, Color, string[])>();
    }

    private List<ChangelogSource> _sources = new List<ChangelogSource>();
    private string[] _titles = new string[0];
    private List<PreparedRelease> _releases = new List<PreparedRelease>();
    private int _source = -1;

    public override string Title => PageTitle;
    public override int Order => 10;

    // Re-read on every visit so a rebuilt changelog shows up without restarting.
    public override void OnShow()
    {
        _sources = ChangelogFeed.Load();
        _titles = new string[_sources.Count];
        for (int i = 0; i < _titles.Length; i++)
            _titles[i] = _sources[i].Title;
        Select(Mathf.Clamp(_source, 0, Mathf.Max(0, _sources.Count - 1)));
    }

    public override void Draw()
    {
        if (_sources.Count == 0)
        {
            Widgets.Text("No changelog found.");
            return;
        }

        if (_titles.Length > 1)
        {
            int picked = Widgets.Choice(null, _source, _titles);
            if (picked != _source)
                Select(picked);
            Widgets.Space();
        }

        foreach (var release in _releases)
        {
            GUILayout.BeginVertical(MenuStyles.Box);
            Widgets.Header(release.Heading);
            foreach (var (title, color, lines) in release.Sections)
            {
                Widgets.Space(4);
                GUILayout.Label(title, MenuStyles.Colored(MenuStyles.Header, color));
                for (int i = 0; i < lines.Length; i++)
                    GUILayout.Label(lines[i], MenuStyles.Text);
            }
            GUILayout.EndVertical();
        }
    }

    private void Select(int index)
    {
        _source = index;
        _releases = new List<PreparedRelease>();
        if (index < 0 || index >= _sources.Count)
            return;

        foreach (var release in _sources[index].Document.Releases)
        {
            var prepared = new PreparedRelease
            {
                Heading = $"Version {release.Version}  <size=12>{MenuPalette.Paint(release.Date, MenuPalette.Muted)}</size>" +
                          (release.Released ? "" : "  <size=11>" + MenuPalette.Paint("in progress", MenuPalette.Warn) + "</size>"),
            };
            AddSection(prepared, "Added", MenuPalette.Good, release.Added);
            AddSection(prepared, "Changed", MenuPalette.Accent, release.Changed);
            AddSection(prepared, "Fixed", MenuPalette.Warn, release.Fixed);
            AddSection(prepared, "Removed", MenuPalette.Bad, release.Removed);
            _releases.Add(prepared);
        }
    }

    private static void AddSection(PreparedRelease release, string title, Color color, List<ChangelogItem> items)
    {
        if (items == null || items.Count == 0)
            return;
        var lines = new string[items.Count];
        for (int i = 0; i < lines.Length; i++)
        {
            string area = string.IsNullOrEmpty(items[i].Area) ? "" : MenuPalette.Paint(items[i].Area + ":", MenuPalette.Muted) + " ";
            lines[i] = "•  " + area + items[i].Text;
        }
        release.Sections.Add((title, color, lines));
    }
}
