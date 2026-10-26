using NeetModKit.Registry;
using UnityEngine;

namespace NeetModKit.Menu;

/// <summary>Draws the menu frame: title bar, tabs, the selected page and the footer.</summary>
internal sealed class MenuWindow
{
    private const float BaseWidth = 980f;
    private const float BaseHeight = 640f;

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

        float w = Mathf.Min(BaseWidth, Screen.width / scale - 20f);
        float h = Mathf.Min(BaseHeight, Screen.height / scale - 20f);
        var area = new Rect((Screen.width / scale - w) / 2f, (Screen.height / scale - h) / 2f, w, h);

        GUILayout.BeginArea(area, MenuStyles.Window);
        DrawTitleBar();
        DrawTabs();
        Widgets.Space(6);

        _scroll = GUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none, MenuStyles.Scrollbar, GUIStyle.none);
        if (_selected != null && Contains(_selected))
            _selected.Draw();
        else
            Widgets.Text("Nothing here yet.");
        GUILayout.EndScrollView();

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
