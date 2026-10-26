using System;
using System.Collections.Generic;
using NeetLoader.Runtime;
using UnityEngine;

namespace NeetModKit.Ui.Toolkit;

/// <summary>
/// A <see cref="UiWindow"/> with a row of tabs, one per page. Pages are built when shown and
/// refreshed four times a second while visible. Used by the mod menu and the cheat menu.
/// </summary>
public sealed class UiTabbedWindow
{
    private const float RefreshInterval = 0.25f;

    private readonly Func<IReadOnlyList<UiPage>> _pages;
    private readonly UiWindow _window;
    private readonly float _tabWidth;
    private UiPage _selected;
    private float _nextRefresh;
    private bool _tabsDirty = true;

    public UiTabbedWindow(ITracker owner, string title, Func<IReadOnlyList<UiPage>> pages, Action onClose,
        float tabWidth = 150f, int sortingOrder = 1000)
    {
        _pages = pages;
        _tabWidth = tabWidth;
        _window = new UiWindow(owner, title, onClose, sortingOrder: sortingOrder);
    }

    public bool Visible => _window.Visible;

    public string SelectedTitle => _selected?.Title;

    /// <summary>Call when pages were added or removed.</summary>
    public void PagesChanged() => _tabsDirty = true;

    public void Select(string title)
    {
        foreach (var page in _pages())
        {
            if (page.Title == title)
                Show(page);
        }
    }

    public void Open()
    {
        if (_selected == null || !Contains(_selected))
        {
            var pages = _pages();
            Show(pages.Count > 0 ? pages[0] : null);
        }
        else
        {
            BuildPage(); // Things may have changed while the window was closed.
        }
        _window.Show();
    }

    public void Close()
    {
        _selected?.OnHide();
        _window.Hide();
    }

    /// <summary>Call every frame while the window is open.</summary>
    public void Tick()
    {
        if (_tabsDirty)
            BuildTabs();
        if (_selected == null)
            return;

        if (_selected.RebuildRequested)
        {
            BuildPage();
        }
        else if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            _selected.Refresh();
        }
    }

    private void Show(UiPage page)
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
        foreach (var page in _pages())
        {
            var target = page;
            _window.Toolbar.Button(page.Title.ToUpperInvariant(), () => Show(target), _tabWidth, accent: page == _selected);
        }
    }

    private bool Contains(UiPage page)
    {
        foreach (var p in _pages())
        {
            if (p == page)
                return true;
        }
        return false;
    }
}
