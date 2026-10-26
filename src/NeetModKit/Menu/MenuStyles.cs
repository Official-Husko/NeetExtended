using UnityEngine;

namespace NeetModKit.Menu;

/// <summary>
/// IMGUI styles for the mod menu. Built once on first use (GUI.skin is only valid inside OnGUI)
/// and reused every frame.
/// </summary>
internal static class MenuStyles
{
    public static readonly Color Accent = new Color(0.95f, 0.55f, 0.75f);
    public static readonly Color Good = new Color(0.45f, 0.85f, 0.5f);
    public static readonly Color Warn = new Color(0.95f, 0.8f, 0.35f);
    public static readonly Color Bad = new Color(0.95f, 0.4f, 0.4f);
    public static readonly Color Muted = new Color(0.65f, 0.65f, 0.7f);

    public static GUIStyle Window, Title, Header, Text, TextLine, Small, Tab, TabActive, Button, ListItem, ListItemActive, Box;
    public static GUIStyle Toggle, Input, Slider, SliderThumb, Scrollbar, ScrollbarThumb;

    /// <summary>Themed copy of the default skin, set as GUI.skin while the menu draws.</summary>
    public static GUISkin Skin;

    private static bool _built;

    public static void Ensure()
    {
        if (_built)
            return;
        _built = true;

        var dark = MenuTextures.Solid(new Color(0.09f, 0.09f, 0.11f, 0.97f));
        var panel = MenuTextures.Solid(new Color(0.13f, 0.13f, 0.16f, 1f));
        var button = MenuTextures.Solid(new Color(0.21f, 0.21f, 0.26f, 1f));
        var hover = MenuTextures.Solid(new Color(0.28f, 0.28f, 0.34f, 1f));
        var active = MenuTextures.Solid(new Color(0.35f, 0.2f, 0.3f, 1f));

        Window = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 12, 12) };
        Window.normal.background = dark;

        Box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8), margin = new RectOffset(0, 0, 4, 4) };
        Box.normal.background = panel;

        Title = Label(20, FontStyle.Bold, Accent);
        Header = Label(15, FontStyle.Bold, Color.white);
        Text = Label(13, FontStyle.Normal, new Color(0.9f, 0.9f, 0.92f));
        Text.wordWrap = true;
        TextLine = new GUIStyle(Text) { wordWrap = false };
        Small = Label(11, FontStyle.Normal, Muted);
        Small.wordWrap = true;

        Button = new GUIStyle(GUI.skin.button) { fontSize = 13, padding = new RectOffset(10, 10, 5, 5) };
        Button.normal.background = button;
        Button.hover.background = hover;
        Button.active.background = active;
        Button.normal.textColor = Button.hover.textColor = Color.white;

        Tab = new GUIStyle(Button) { fontSize = 14 };
        TabActive = new GUIStyle(Tab);
        TabActive.normal.background = TabActive.hover.background = active;
        TabActive.normal.textColor = TabActive.hover.textColor = Accent;

        BuildControls();

        ListItem = new GUIStyle(Button) { alignment = TextAnchor.MiddleLeft };
        ListItemActive = new GUIStyle(ListItem);
        ListItemActive.normal.background = ListItemActive.hover.background = active;
    }

    private static readonly Color Field = new Color(0.07f, 0.07f, 0.09f, 1f);
    private static readonly Color Edge = new Color(0.32f, 0.32f, 0.38f, 1f);

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
            MenuTextures.Check(Accent, new Color(0.1f, 0.05f, 0.08f));

        Input = Skin.textField;
        Input.fontSize = 13;
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
