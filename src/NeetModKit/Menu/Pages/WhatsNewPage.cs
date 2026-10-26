using System.Collections.Generic;
using NeetModKit.Changelog;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>Release notes from changelog.json, newest first.</summary>
internal sealed class WhatsNewPage : MenuPage
{
    public const string PageTitle = "What's New";

    private List<ChangelogSource> _sources = new List<ChangelogSource>();
    private string[] _titles = new string[0];
    private int _source;

    public override string Title => PageTitle;
    public override int Order => 10;

    // Re-read on every visit so a rebuilt changelog shows up without restarting.
    public override void OnShow()
    {
        _sources = ChangelogFeed.Load();
        _titles = new string[_sources.Count];
        for (int i = 0; i < _titles.Length; i++)
            _titles[i] = _sources[i].Title;
        if (_source >= _sources.Count)
            _source = 0;
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
            _source = Widgets.Choice(null, _source, _titles);
            Widgets.Space();
        }

        foreach (var release in _sources[_source].Document.Releases)
            DrawRelease(release);
    }

    private static void DrawRelease(ChangelogRelease release)
    {
        GUILayout.BeginVertical(MenuStyles.Box);
        Widgets.Header(release.Heading);
        DrawSection("Added", release.Added, MenuStyles.Good);
        DrawSection("Changed", release.Changed, MenuStyles.Accent);
        DrawSection("Fixed", release.Fixed, MenuStyles.Warn);
        DrawSection("Removed", release.Removed, MenuStyles.Bad);
        GUILayout.EndVertical();
    }

    private static void DrawSection(string name, List<ChangelogItem> items, Color color)
    {
        if (items == null || items.Count == 0)
            return;
        Widgets.Space(4);
        GUILayout.Label(name, MenuStyles.Colored(MenuStyles.Header, color));
        for (int i = 0; i < items.Count; i++)
            GUILayout.Label(items[i].Line, MenuStyles.Text);
    }
}
