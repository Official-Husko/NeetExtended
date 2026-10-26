using NeetModKit.Registry;
using UnityEngine;

namespace NeetModKit.Menu;

/// <summary>Draws the menu frame: title bar, tabs, the selected page and the footer.</summary>
internal sealed class MenuWindow
{
    // Share of the game window the menu takes, and the smallest size it shrinks to.
    private const float WidthShare = 0.9f;
    private const float HeightShare = 0.88f;
    private const float MinWidth = 760f;
    private const float MinHeight = 480f;

    private readonly OwnedList<MenuPage> _pages;
    private MenuPage _selected;
    private Vector2 _scroll;

    public MenuWindow(OwnedList<MenuPage> pages)
    {
        _pages = pages;
    }

    public void Select(string title)
    {
        foreach (var page in _pages.Items)
        {
            if (page.Title == title)
                Show(page);
        }
    }

    public void OnOpened()
    {
        if (_selected == null || !Contains(_selected))
            _selected = _pages.Items.Count > 0 ? _pages.Items[0] : null;
        _selected?.OnShow();
    }

    public void OnClosed() => _selected?.OnHide();

    public void Draw()
    {
        MenuStyles.Ensure();
        var previousSkin = GUI.skin;
        GUI.skin = MenuStyles.Skin;

        float scale = Mathf.Clamp(KitMod.Config.Value.MenuScale, 0.6f, 2f) * Screen.height / 1080f;
        var previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float screenW = Screen.width / scale, screenH = Screen.height / scale;
        float w = Mathf.Min(screenW, Mathf.Max(MinWidth, screenW * WidthShare));
        float h = Mathf.Min(screenH, Mathf.Max(MinHeight, screenH * HeightShare));
        var area = new Rect((screenW - w) / 2f, (screenH - h) / 2f, w, h);

        GUILayout.BeginArea(area, MenuStyles.Window);
        DrawTitleBar();
        DrawTabs();
        Widgets.Space(6);

        if (_selected == null || !Contains(_selected))
        {
            Widgets.Text("Nothing here yet.");
        }
        else if (_selected.FillsHeight)
        {
            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            _selected.Draw();
            GUILayout.EndVertical();
        }
        else
        {
            _scroll = Widgets.BeginScroll(_scroll);
            _selected.Draw();
            GUILayout.EndScrollView();
        }

        GUILayout.EndArea();
        GUI.matrix = previousMatrix;
        GUI.skin = previousSkin;
    }

    private void DrawTitleBar()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("NEET Mods", MenuStyles.Title);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Loader {NeetLoader.Loader.Version}  ·  {KitMod.Config.Value.MenuKey} to close", MenuStyles.TextLine);
        if (Widgets.Button("Close", 70))
            ModMenu.Close();
        GUILayout.EndHorizontal();
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal();
        foreach (var page in _pages.Items)
        {
            if (GUILayout.Button(page.Title, page == _selected ? MenuStyles.TabActive : MenuStyles.Tab))
                Show(page);
        }
        GUILayout.EndHorizontal();
    }

    private void Show(MenuPage page)
    {
        if (page == _selected)
            return;
        _selected?.OnHide();
        _selected = page;
        _scroll = Vector2.zero;
        page.OnShow();
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
