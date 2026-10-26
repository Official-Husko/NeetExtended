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

    private static bool _built;

    public static void Ensure()
    {
        if (_built)
            return;
        _built = true;

        var dark = Solid(new Color(0.09f, 0.09f, 0.11f, 0.97f));
        var panel = Solid(new Color(0.13f, 0.13f, 0.16f, 1f));
        var button = Solid(new Color(0.21f, 0.21f, 0.26f, 1f));
        var hover = Solid(new Color(0.28f, 0.28f, 0.34f, 1f));
        var active = Solid(new Color(0.35f, 0.2f, 0.3f, 1f));

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

        ListItem = new GUIStyle(Button) { alignment = TextAnchor.MiddleLeft };
        ListItemActive = new GUIStyle(ListItem);
        ListItemActive.normal.background = ListItemActive.hover.background = active;
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

    private static Texture2D Solid(Color color)
    {
        var tex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }
}
