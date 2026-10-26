using System.Collections.Generic;
using NeetModKit.Changelog;
using NeetModKit.Ui.Toolkit;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>Release notes from changelog.json, newest first. Mods that ship their own changelog get a tab.</summary>
internal sealed class WhatsNewPage : MenuPage
{
    public const string PageTitle = "What's New";

    private List<ChangelogSource> _sources = new List<ChangelogSource>();
    private int _source;

    public override string Title => PageTitle;
    public override int Order => 10;

    public override void Build(UiBuilder ui)
    {
        // Re-read each time so a rebuilt changelog shows up without restarting.
        _sources = ChangelogFeed.Load();
        if (_sources.Count == 0)
        {
            ui.Text("No changelog found.");
            return;
        }
        _source = Mathf.Clamp(_source, 0, _sources.Count - 1);

        if (_sources.Count > 1)
        {
            var tabs = ui.Row(6f, 26f);
            for (int i = 0; i < _sources.Count; i++)
            {
                int index = i;
                tabs.Button(_sources[i].Title.ToUpperInvariant(), () =>
                {
                    _source = index;
                    Rebuild();
                }, 180f, accent: i == _source);
            }
        }

        var list = ui.Scroll(out _);
        foreach (var release in _sources[_source].Document.Releases)
            BuildRelease(list, release);
    }

    private static void BuildRelease(UiBuilder list, ChangelogRelease release)
    {
        var panel = list.Panel();
        panel.Header("VERSION " + release.Version);
        panel.Text(MenuPalette.Paint(release.Date, MenuPalette.Muted) +
                   (release.Released ? "" : "  " + MenuPalette.Paint("in progress", MenuPalette.Warn)));
        Section(panel, "ADDED", MenuPalette.Good, release.Added);
        Section(panel, "CHANGED", MenuPalette.Accent, release.Changed);
        Section(panel, "FIXED", MenuPalette.Warn, release.Fixed);
        Section(panel, "REMOVED", MenuPalette.Bad, release.Removed);
    }

    private static void Section(UiBuilder panel, string title, Color color, List<ChangelogItem> items)
    {
        if (items == null || items.Count == 0)
            return;
        panel.SubHeader(title).color = color;
        foreach (var item in items)
        {
            string area = string.IsNullOrEmpty(item.Area) ? "" : MenuPalette.Paint(item.Area + ":", MenuPalette.Muted) + " ";
            panel.Text("•  " + area + item.Text);
        }
    }
}
