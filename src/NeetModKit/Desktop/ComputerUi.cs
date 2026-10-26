using System;
using NeetLoader;
using NeetLoader.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
        var go = Clone(owner, template, name);
        var button = go.GetComponent<Button>() ?? throw new ArgumentException($"{template.name} isn't a button.");
        button.onClick.AddListener(() => onClick());
        ComputerControl.Main?.RegisterButtonClickAudio(button);
        SetText(go.transform, label);
        return button;
    }

    /// <summary>Copies a text element (TextMeshPro or legacy Text).</summary>
    public static TMP_Text CloneLabel(NeetMod owner, Transform template, string name, string text)
    {
        var go = Clone(owner, template, name);
        var label = go.GetComponentInChildren<TMP_Text>(true) ?? throw new ArgumentException($"{template.name} has no TextMeshPro text.");
        label.text = text;
        return label;
    }

    /// <summary>Sets the first text found on or under the object.</summary>
    public static void SetText(Transform target, string text)
    {
        var tmp = target.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = text;
            return;
        }
        var legacy = target.GetComponentInChildren<Text>(true);
        if (legacy != null)
            legacy.text = text;
    }

    /// <summary>Places a RectTransform relative to its anchors.</summary>
    public static void Place(Component element, Vector2 anchoredPosition, Vector2? size = null)
    {
        var rect = (RectTransform)element.transform;
        rect.anchoredPosition = anchoredPosition;
        if (size.HasValue)
            rect.sizeDelta = size.Value;
    }

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

    private static GameObject Clone(NeetMod owner, Transform template, string name)
    {
        var go = Object.Instantiate(template.gameObject, template.parent);
        go.name = name;
        go.SetActive(true);
        owner.Track(Disposable.Destroying(go));
        return go;
    }
}
