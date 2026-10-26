using UnityEngine;

namespace NeetModKit.Menu;

/// <summary>
/// IMGUI styles for the mod menu. Built once on first use (GUI.skin is only valid inside OnGUI)
/// and reused every frame.
/// </summary>
internal static class MenuStyles
{
    // Short aliases for the palette, used all over the pages.
    public static Color Accent => MenuPalette.Accent;
    public static Color Good => MenuPalette.Good;
    public static Color Warn => MenuPalette.Warn;
    public static Color Bad => MenuPalette.Bad;
    public static Color Muted => MenuPalette.Muted;

    public static GUIStyle Window, Title, Header, Text, TextLine, Small, Tab, TabActive, Button, ListItem, ListItemActive, Box;
    public static GUIStyle Toggle, Input, Slider, SliderThumb, Scrollbar, ScrollbarThumb;

    /// <summary>Themed copy of the default skin, set as GUI.skin while the menu draws.</summary>
    public static GUISkin Skin;

    private static bool _built;

    /// <summary>Frees the generated textures and skin; styles are rebuilt on next use.</summary>
    public static void Release()
    {
        MenuTextures.ReleaseAll();
        if (Skin != null)
            Object.Destroy(Skin);
        Skin = null;
        ColoredCache.Clear();
        _built = false;
    }

    public static void Ensure()
    {
        if (_built)
            return;
        _built = true;

        var panel = MenuTextures.Bordered(MenuPalette.Panel, Color.Lerp(MenuPalette.Panel, MenuPalette.Edge, 0.5f));
        var button = MenuTextures.Solid(MenuPalette.Button);
        var hover = MenuTextures.Solid(MenuPalette.ButtonHover);
        var active = MenuTextures.Solid(MenuPalette.Selected);
        var one = new RectOffset(1, 1, 1, 1);

        Window = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 12, 12), border = one };
        Window.normal.background = MenuTextures.Bordered(MenuPalette.Background, MenuPalette.Accent * 0.8f);

        Box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8), margin = new RectOffset(0, 0, 4, 4), border = one };
        Box.normal.background = panel;

        Title = Label(22, FontStyle.Bold, Accent);
        Header = Label(16, FontStyle.Bold, Color.white);
        Text = Label(14, FontStyle.Normal, MenuPalette.Text);
        Text.wordWrap = true;
        TextLine = new GUIStyle(Text) { wordWrap = false };
        Small = Label(12, FontStyle.Normal, Muted);
        Small.wordWrap = true;

        Button = new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(10, 10, 5, 5) };
        Button.normal.background = button;
        Button.hover.background = hover;
        Button.active.background = active;
        Button.normal.textColor = Button.hover.textColor = Color.white;

        Tab = new GUIStyle(Button) { fontSize = 15 };
        TabActive = new GUIStyle(Tab);
        TabActive.normal.background = TabActive.hover.background = active;
        TabActive.normal.textColor = TabActive.hover.textColor = Accent;

        BuildControls();

        ListItem = new GUIStyle(Button) { alignment = TextAnchor.MiddleLeft };
        ListItemActive = new GUIStyle(ListItem);
        ListItemActive.normal.background = ListItemActive.hover.background = active;
    }

    private static Color Field => MenuPalette.Field;
    private static Color Edge => MenuPalette.Edge;

    /// <summary>
    /// Themes the controls on a copy of the default skin. Unity looks scrollbar parts up by
    /// style name in the active skin, so the styles are edited in place rather than replaced.
    /// The skin is only active while the menu draws, so mod pages get the theme for free.
    /// </summary>
    private static void BuildControls()
    {
        Skin = Object.Instantiate(GUI.skin);
        Skin.hideFlags = HideFlags.HideAndDontSave;

        var fieldTex = MenuTextures.Bordered(Field, Edge);
        var fieldFocus = MenuTextures.Bordered(Field, Accent);
        var one = new RectOffset(1, 1, 1, 1);

        Toggle = Skin.toggle;
        Toggle.fixedWidth = Toggle.fixedHeight = 18;
        Toggle.margin = new RectOffset(0, 8, 3, 3);
        Toggle.padding = new RectOffset(0, 0, 0, 0);
        Toggle.overflow = new RectOffset(0, 0, 0, 0);
        Toggle.border = one;
        Toggle.normal.background = fieldTex;
        Toggle.hover.background = Toggle.focused.background = fieldFocus;
        Toggle.active.background = MenuTextures.Bordered(Field, Color.white);
        Toggle.onNormal.background = Toggle.onHover.background = Toggle.onFocused.background = Toggle.onActive.background =
            MenuTextures.Check(Accent, new Color(0.12f, 0.05f, 0.02f));

        Input = Skin.textField;
        Input.fontSize = 14;
        Input.padding = new RectOffset(8, 8, 5, 5);
        Input.border = one;
        Input.normal.background = Input.hover.background = fieldTex;
        Input.focused.background = Input.onFocused.background = fieldFocus;
        Input.normal.textColor = Input.hover.textColor = Input.focused.textColor = Input.onFocused.textColor = Color.white;

        Slider = Skin.horizontalSlider;
        Slider.fixedHeight = 6;
        Slider.margin = new RectOffset(4, 4, 10, 10);
        Slider.padding = new RectOffset(0, 0, 0, 0);
        Slider.border = one;
        Slider.normal.background = fieldTex;

        SliderThumb = Skin.horizontalSliderThumb;
        SliderThumb.fixedWidth = 12;
        SliderThumb.fixedHeight = 18;
        SliderThumb.overflow = new RectOffset(0, 0, 6, 6);
        SliderThumb.normal.background = MenuTextures.Solid(Accent);
        SliderThumb.hover.background = SliderThumb.active.background = SliderThumb.focused.background =
            MenuTextures.Solid(Color.Lerp(Accent, Color.white, 0.3f));

        Scrollbar = Skin.verticalScrollbar;
        Scrollbar.fixedWidth = 8;
        Scrollbar.margin = new RectOffset(6, 0, 0, 0);
        Scrollbar.border = new RectOffset(0, 0, 0, 0);
        Scrollbar.normal.background = MenuTextures.Solid(Field);

        ScrollbarThumb = Skin.verticalScrollbarThumb;
        ScrollbarThumb.fixedWidth = 8;
        ScrollbarThumb.border = new RectOffset(0, 0, 0, 0);
        ScrollbarThumb.normal.background = MenuTextures.Solid(Edge);
        ScrollbarThumb.hover.background = ScrollbarThumb.active.background = MenuTextures.Solid(Accent);

        // No arrow buttons on the scrollbar.
        Skin.verticalScrollbarUpButton.fixedHeight = Skin.verticalScrollbarDownButton.fixedHeight = 0;
        Skin.verticalScrollbarUpButton.normal.background = Skin.verticalScrollbarDownButton.normal.background = null;
    }

    private static readonly System.Collections.Generic.Dictionary<(GUIStyle, Color), GUIStyle> ColoredCache =
        new System.Collections.Generic.Dictionary<(GUIStyle, Color), GUIStyle>();

    /// <summary>A copy of a style in another text colour. Cached, so it's safe to call every frame.</summary>
    public static GUIStyle Colored(GUIStyle baseStyle, Color color)
    {
        if (ColoredCache.TryGetValue((baseStyle, color), out var cached))
            return cached;
        var s = new GUIStyle(baseStyle);
        s.normal.textColor = color;
        ColoredCache[(baseStyle, color)] = s;
        return s;
    }

    private static GUIStyle Label(int size, FontStyle style, Color color)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style, richText = true };
        s.normal.textColor = color;
        return s;
    }

}
