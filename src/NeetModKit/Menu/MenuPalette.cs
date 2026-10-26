using UnityEngine;

namespace NeetModKit.Menu;

/// <summary>
/// Colours for state and emphasis, matched to the game's menus (orange headers, violet buttons)
/// with Halloween accents: slime green for good, candy-corn yellow for warnings, blood red for errors.
/// </summary>
public static class MenuPalette
{
    public static readonly Color Accent = new Color(1f, 0.25f, 0f);
    public static readonly Color Good = new Color(0.56f, 0.9f, 0.29f);
    public static readonly Color Warn = new Color(1f, 0.8f, 0.22f);
    public static readonly Color Bad = new Color(0.95f, 0.25f, 0.25f);
    public static readonly Color Muted = new Color(0.7f, 0.64f, 0.78f);
    public static readonly Color Text = Color.white;

    /// <summary>"#rrggbb" for rich-text tags.</summary>
    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    /// <summary>Wraps text in a rich-text colour tag.</summary>
    public static string Paint(string text, Color c) => $"<color={Hex(c)}>{text}</color>";
}
