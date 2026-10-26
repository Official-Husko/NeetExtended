using System;
using NeetLoader;
using NeetLoader.Runtime;
using NeetModKit.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Desktop;

/// <summary>
/// Builds UI on the computer screen by copying the game's own elements, so new buttons and labels
/// match the look of the site they're on. Everything created is removed when the mod unloads.
/// </summary>
public static class ComputerUi
{
    /// <summary>Copies a button, sets its label and click action, and gives it the computer's click sound.</summary>
    public static Button CloneButton(NeetMod owner, Transform template, string name, string label, Action onClick)
    {
        var go = UiCloning.Clone(owner, template, name);
        var button = go.GetComponent<Button>() ?? throw new ArgumentException($"{template.name} isn't a button.");
        UiCloning.SetClick(button, onClick);
        ComputerControl.Main?.RegisterButtonClickAudio(button);
        UiCloning.SetText(go.transform, label);
        return button;
    }

    /// <summary>Copies a TextMeshPro label.</summary>
    public static TMP_Text CloneLabel(NeetMod owner, Transform template, string name, string text)
    {
        var go = UiCloning.Clone(owner, template, name);
        var label = go.GetComponentInChildren<TMP_Text>(true) ?? throw new ArgumentException($"{template.name} has no TextMeshPro text.");
        label.text = text;
        return label;
    }

    public static void SetText(Transform target, string text) => UiCloning.SetText(target, text);

    public static void Place(Component element, Vector2 anchoredPosition, Vector2? size = null) =>
        UiCloning.Place(element, anchoredPosition, size);

    /// <summary>Moves the top edge of a stretched element down (positive) or up, undone on unload.</summary>
    public static void PushTopEdge(NeetMod owner, RectTransform rect, float pixels)
    {
        rect.offsetMax -= new Vector2(0f, pixels);
        owner.Track(Disposable.Create(() =>
        {
            if (rect != null)
                rect.offsetMax += new Vector2(0f, pixels);
        }));
    }
}
