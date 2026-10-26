using System;
using UnityEngine;

namespace NeetModKit.Menu;

/// <summary>Small layout helpers so pages read as a list of rows instead of raw GUILayout calls.</summary>
public static class Widgets
{
    public static void Header(string text) => GUILayout.Label(text, MenuStyles.Header);

    public static void Text(string text) => GUILayout.Label(text, MenuStyles.Text);

    public static void Small(string text) => GUILayout.Label(text, MenuStyles.Small);

    public static void Space(float pixels = 8f) => GUILayout.Space(pixels);

    public static bool Button(string text, float width = 0f) =>
        width > 0f ? GUILayout.Button(text, MenuStyles.Button, GUILayout.Width(width)) : GUILayout.Button(text, MenuStyles.Button);

    public static bool Toggle(string label, bool value)
    {
        GUILayout.BeginHorizontal();
        bool result = GUILayout.Toggle(value, GUIContent.none, MenuStyles.Toggle);
        if (GUILayout.Button(label, MenuStyles.TextLine))
            result = !result;
        GUILayout.EndHorizontal();
        return result;
    }

    public static float Slider(string label, float value, float min, float max, string format = "0.##")
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, MenuStyles.TextLine, GUILayout.Width(220));
        float result = GUILayout.HorizontalSlider(value, min, max, MenuStyles.Slider, MenuStyles.SliderThumb, GUILayout.ExpandWidth(true));
        GUILayout.Label(result.ToString(format), MenuStyles.Text, GUILayout.Width(60));
        GUILayout.EndHorizontal();
        return result;
    }

    public static string TextField(string label, string value, float labelWidth = 220f)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, MenuStyles.TextLine, GUILayout.Width(labelWidth));
        string result = GUILayout.TextField(value ?? "", MenuStyles.Input, GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
        return result;
    }

    /// <summary>A row of buttons, one per option; returns the clicked index or the current one.</summary>
    public static int Choice(string label, int current, string[] options)
    {
        GUILayout.BeginHorizontal();
        if (label != null)
            GUILayout.Label(label, MenuStyles.TextLine, GUILayout.Width(220));
        int result = GUILayout.SelectionGrid(current, options, Math.Min(options.Length, 6), MenuStyles.Button);
        GUILayout.EndHorizontal();
        return result;
    }

    /// <summary>Wraps a block in a panel.</summary>
    public static void Section(string title, Action body)
    {
        GUILayout.BeginVertical(MenuStyles.Box);
        if (title != null)
            Header(title);
        body();
        GUILayout.EndVertical();
    }
}
