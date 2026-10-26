using NeetModKit.Registry;
using NeetModKit.Ui.Toolkit;
using UnityEngine;

namespace NeetModKit.Menu;

/// <summary>The mod menu's window: tabs along the top, the selected page below.</summary>
internal sealed class MenuWindow
{
    private const float RefreshInterval = 0.25f;
    private const float TabWidth = 150f;

    private readonly OwnedList<MenuPage> _pages;
    private readonly UiWindow _window;
    private MenuPage _selected;
    private float _nextRefresh;
    private bool _tabsDirty = true;

    public MenuWindow(NeetLoader.NeetMod owner, OwnedList<MenuPage> pages)
    {
        _pages = pages;
        _window = new UiWindow(owner, "NEET MODS", ModMenu.Close);
        _pages.Changed += () => _tabsDirty = true;
    }

    public string SelectedTitle => _selected?.Title;

    public void Select(string title)
    {
        foreach (var page in _pages.Items)
        {
            if (page.Title == title)
                Show(page);
        }
    }

    public void Open()
    {
        if (_selected == null || !Contains(_selected))
            Show(_pages.Items.Count > 0 ? _pages.Items[0] : null);
        else
            BuildPage(); // Things may have changed while the menu was closed.
        _window.Show();
    }

    public void Close()
    {
        _selected?.OnHide();
        _window.Hide();
    }

    /// <summary>Runs every frame while the menu is open.</summary>
    public void Tick()
    {
        if (_tabsDirty)
            BuildTabs();

        if (_selected == null)
            return;
        if (_selected.RebuildRequested)
            BuildPage();
        else if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            _selected.Refresh();
        }
    }

    private void Show(MenuPage page)
    {
        if (page == _selected && page != null)
            return;
        _selected?.OnHide();
        _selected = page;
        _tabsDirty = true;
        BuildPage();
    }

    private void BuildPage()
    {
        _window.Content.Clear();
        if (_selected == null)
            return;
        _selected.RebuildRequested = false;
        _selected.Build(_window.Content);
        _selected.Refresh();
        _nextRefresh = Time.unscaledTime + RefreshInterval;
    }

    private void BuildTabs()
    {
        _tabsDirty = false;
        _window.Toolbar.Clear();
        foreach (var page in _pages.Items)
        {
            var target = page;
            _window.Toolbar.Button(page.Title.ToUpperInvariant(), () => Show(target), TabWidth, accent: page == _selected);
        }
    }

    private bool Contains(MenuPage page)
    {
        foreach (var p in _pages.Items)
        {
            if (p == page)
                return true;
        }
        return false;
    }
}
