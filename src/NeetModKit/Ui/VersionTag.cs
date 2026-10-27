using System;
using System.Collections.Generic;
using NeetLoader.Runtime;
using NeetModKit.About;
using NeetModKit.Ui.Toolkit;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui;

/// <summary>
/// The pack's name, version and commit in the bottom left corner of every screen, so players can
/// see the mods are running and screenshots show which build they came from. Says so when the game
/// isn't the build this version was tested with (<see cref="GameCompat"/>). Mods can add short
/// notes after the name with <see cref="AddNote"/>. Doesn't take clicks.
/// </summary>
public static class VersionTag
{
    private const string Separator = "  ·  ";
    private const int FontSize = 15;
    private static readonly Color Colour = new Color(0.55f, 0.55f, 0.55f, 0.7f);
    private static readonly Vector2 Margin = new Vector2(10f, 6f);

    private sealed class Note : IDisposable
    {
        public string Text;

        public void Dispose()
        {
            if (Notes.Remove(this))
                Refresh();
        }
    }

    private static readonly List<Note> Notes = new List<Note>();
    private static GameObject _root;
    private static Text _label;

    /// <summary>
    /// Adds a note after the pack's name and version ("NeetExtended v0.2.0  ·  update available (v0.3.0)").
    /// Dispose the result to take it back; it also goes when <paramref name="owner"/> unloads.
    /// </summary>
    public static IDisposable AddNote(ITracker owner, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("A note needs some text.", nameof(text));
        var note = new Note { Text = text.Trim() };
        Notes.Add(note);
        Refresh();
        return owner.Track(note);
    }

    internal static void Create(ITracker owner, bool visible)
    {
        // Under the mod menu's windows (1000), over the game's own screens.
        var canvas = UiCanvas.Create(owner, "NeetModKit.VersionTag", sortingOrder: 900, interactive: false,
            referenceResolution: new Vector2(1920f, 1080f));
        _root = canvas.gameObject;

        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
        label.transform.SetParent(canvas.transform, false);
        _label = label;
        if (GameCompat.Status == GameCompatStatus.Untested)
            AddNote(owner, "game build not tested");
        else
            Refresh();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = FontSize;
        label.color = Colour;
        label.alignment = TextAnchor.LowerLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.raycastTarget = false;

        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = Margin;
        rect.sizeDelta = new Vector2(600f, FontSize * 2f);

        SetVisible(visible);
    }

    internal static void SetVisible(bool visible)
    {
        if (_root != null && _root.activeSelf != visible)
            _root.SetActive(visible);
    }

    /// <summary>Writes the label again; notes can come and go at any time, and the label may not exist yet.</summary>
    private static void Refresh()
    {
        if (_label == null)
            return;
        string text = PackInfo.Label;
        foreach (var note in Notes)
            text += Separator + note.Text;
        _label.text = text;
    }
}
