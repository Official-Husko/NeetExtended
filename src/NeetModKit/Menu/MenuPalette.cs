using UnityEngine;

namespace NeetModKit.Menu;

/// <summary>
/// Menu colours in one place. NEET is a Halloween game, so: pumpkin orange on near-black purple,
/// slime green for good news, candy-corn yellow for warnings, blood red for errors.
/// </summary>
public static class MenuPalette
{
    public static readonly Color Background = new Color(0.055f, 0.035f, 0.07f, 0.97f);
    public static readonly Color Panel = new Color(0.11f, 0.07f, 0.14f, 1f);
    public static readonly Color Button = new Color(0.19f, 0.11f, 0.23f, 1f);
    public static readonly Color ButtonHover = new Color(0.27f, 0.15f, 0.32f, 1f);
    public static readonly Color Selected = new Color(0.42f, 0.18f, 0.04f, 1f);
    public static readonly Color Field = new Color(0.04f, 0.03f, 0.05f, 1f);
    public static readonly Color Edge = new Color(0.33f, 0.2f, 0.4f, 1f);

    public static readonly Color Accent = new Color(1f, 0.47f, 0.1f);
    public static readonly Color Good = new Color(0.56f, 0.9f, 0.29f);
    public static readonly Color Warn = new Color(1f, 0.8f, 0.22f);
    public static readonly Color Bad = new Color(0.92f, 0.22f, 0.25f);
    public static readonly Color Muted = new Color(0.66f, 0.6f, 0.72f);
    public static readonly Color Text = new Color(0.93f, 0.9f, 0.95f);

    /// <summary>"#rrggbb" for rich-text tags.</summary>
    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    /// <summary>Wraps text in a rich-text colour tag.</summary>
    public static string Paint(string text, Color c) => $"<color={Hex(c)}>{text}</color>";
}
