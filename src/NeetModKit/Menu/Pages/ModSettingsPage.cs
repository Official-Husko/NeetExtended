using System.Collections.Generic;
using NeetLoader.Mods;
using NeetModKit.Settings;
using NeetModKit.Sharing;
using NeetModKit.Ui.Toolkit;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>
/// Every mod that added a page or has settings, in a list like the Mods tab. On the right, the
/// selected mod's page; when it has more than one view (pages and settings), a row to switch them.
/// </summary>
internal sealed class ModSettingsPage : MenuPage
{
    public const string PageTitle = "Mod Settings";
    private const string SettingsView_ = "SETTINGS";
    private const float ListWidth = 250f;

    private sealed class Entry
    {
        public string Id;
        public string Name;
        public readonly List<MenuPage> Pages = new List<MenuPage>();
        public readonly List<SettingsSection> Sections = new List<SettingsSection>();
        public int Views => Pages.Count + (Sections.Count > 0 ? 1 : 0);
    }

    private static ModSettingsPage _instance;

    private readonly SettingsView _settings = new SettingsView();
    private readonly List<Entry> _entries = new List<Entry>();
    private string _selectedId;
    private int _view;
    private MenuPage _shown;
    private bool _settingsShown;
    private int _builtFor = -1;

    public ModSettingsPage() => _instance = this;

    public override string Title => PageTitle;
    public override int Order => 5;

    /// <summary>Selects the mod page called <paramref name="pageTitle"/>; false when no mod has one.</summary>
    public static bool Show(string pageTitle)
    {
        var page = _instance;
        if (page == null)
            return false;
        page.Collect();
        foreach (var entry in page._entries)
        {
            int index = entry.Pages.FindIndex(p => p.Title == pageTitle);
            if (index < 0)
                continue;
            page.Select(entry, index);
            return true;
        }
        return false;
    }

    public static void PagesChanged() => _instance?.Rebuild();

    public override void Build(UiBuilder ui)
    {
        _builtFor = ModControl.Version;
        Collect();
        var selected = _entries.Find(e => e.Id == _selectedId) ?? (_entries.Count > 0 ? _entries[0] : null);
        _selectedId = selected?.Id;

        var body = ui.Row(12f);
        UiBuilder.Size(body.Root, flexibleHeight: 1f);
        BuildList(body.Column(ListWidth, flexibleHeight: 1f), selected);
        BuildSelected(body.Column(flexibleWidth: 1f, flexibleHeight: 1f), selected);
    }

    public override void Refresh()
    {
        if (_shown != null)
        {
            if (_shown.RebuildRequested)
                Rebuild();
            else
                _shown.Refresh();
            return;
        }
        if (!_settingsShown)
            return;
        _settings.Refresh();
        // Another game decided something (co-op host): show it.
        if (!_settings.IsDirty && _builtFor != ModControl.Version)
            Rebuild();
    }

    public override void OnHide() => Leave();

    private void BuildList(UiBuilder column, Entry selected)
    {
        column.SubHeader("MODS");
        var list = column.Scroll(out _);
        foreach (var entry in _entries)
        {
            var target = entry;
            list.Button(entry.Name, () => Select(target, 0), -1f, accent: entry == selected);
        }
    }

    private void BuildSelected(UiBuilder column, Entry entry)
    {
        Leave();
        if (entry == null)
        {
            column.Text("No mod has settings or a page to show.");
            return;
        }
        if (_view >= entry.Views)
            _view = 0;
        if (entry.Views > 1)
        {
            var views = column.Row(6f, 26f);
            for (int i = 0; i < entry.Views; i++)
            {
                int index = i;
                string label = i < entry.Pages.Count ? entry.Pages[i].Title.ToUpperInvariant() : SettingsView_;
                views.Button(label, () => Select(entry, index), 150f, accent: i == _view);
            }
        }
        var content = column.Column(flexibleWidth: 1f, flexibleHeight: 1f);
        if (_view < entry.Pages.Count)
        {
            _shown = entry.Pages[_view];
            _shown.RebuildRequested = false;
            _shown.Build(content);
            _shown.Refresh();
        }
        else
        {
            _settingsShown = true;
            _settings.Build(content, entry.Sections);
        }
    }

    private void Select(Entry entry, int view)
    {
        _selectedId = entry.Id;
        _view = view;
        Rebuild();
    }

    // The page or settings that were showing are going away.
    private void Leave()
    {
        _shown?.OnHide();
        _shown = null;
        if (_settingsShown)
            _settings.Flush();
        _settingsShown = false;
    }

    private void Collect()
    {
        _entries.Clear();
        foreach (var modPage in ModMenu.ModPages)
            Get(modPage.Owner.Info.Id, modPage.Owner.Info.Manifest.DisplayName).Pages.Add(modPage.Page);
        foreach (var section in ModSettings.Sections)
            Get(section.OwnerId ?? section.Title, NameOf(section)).Sections.Add(section);
        _entries.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
    }

    private Entry Get(string id, string name)
    {
        var entry = _entries.Find(e => e.Id == id);
        if (entry == null)
            _entries.Add(entry = new Entry { Id = id, Name = name });
        return entry;
    }

    private static string NameOf(SettingsSection section)
    {
        var mod = section.OwnerId != null ? ModRegistry.Find(section.OwnerId) : null;
        return mod != null ? mod.Manifest.DisplayName : section.Title;
    }
}
