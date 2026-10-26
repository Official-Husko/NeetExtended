using System;
using NeetLoader;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui.Toolkit;

/// <summary>
/// A full-screen window in the style of the game's pause menu: dimmed backdrop, title, close
/// button and a content area. Hidden until shown; destroyed with its owner mod.
/// </summary>
public sealed class UiWindow
{
    private readonly GameObject _root;

    /// <summary>Builder for the bar under the title (tabs, actions).</summary>
    public UiBuilder Toolbar { get; }

    /// <summary>Builder for the main area. Fill it once, or clear and refill it.</summary>
    public UiBuilder Content { get; }

    public Text TitleText { get; }

    public bool Visible => _root.activeSelf;

    public UiWindow(NeetMod owner, string title, Action onClose, float widthShare = 0.92f, float heightShare = 0.9f)
    {
        if (!UiTemplates.Ensure())
            throw new InvalidOperationException("The game's menu isn't loaded yet, so there's nothing to build the window from.");

        var canvas = UiCanvas.Create(owner, "NeetModKit.Window." + title);
        _root = canvas.gameObject;

        var backdrop = UnityEngine.Object.Instantiate(UiTemplates.Get(UiTemplate.Backdrop), canvas.transform, false);
        UiBuilder.Stretch((RectTransform)backdrop.transform);

        var frame = new UiBuilder((RectTransform)canvas.transform).NewChild("Frame");
        frame.anchorMin = new Vector2((1f - widthShare) / 2f, (1f - heightShare) / 2f);
        frame.anchorMax = new Vector2(1f - (1f - widthShare) / 2f, 1f - (1f - heightShare) / 2f);
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        var ui = UiBuilder.Vertical(frame, 8f);

        var titleRow = ui.Row(12f, 34f);
        TitleText = titleRow.Header(title);
        titleRow.FlexibleSpace();
        titleRow.Button("CLOSE", onClose, 110f, accent: true);

        Toolbar = ui.Row(6f, 26f);
        Content = ui.Column(flexibleWidth: 1f, flexibleHeight: 1f);

        Hide();
    }

    public void Show() => _root.SetActive(true);

    public void Hide() => _root.SetActive(false);
}
