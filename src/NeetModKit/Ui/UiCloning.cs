using System;
using NeetLoader;
using NeetLoader.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NeetModKit.Ui;

/// <summary>
/// Copying the game's own uGUI elements so additions look native. Shared by the computer helpers,
/// the pause menu integration and the UI toolkit. Everything created is destroyed with its owner.
/// </summary>
public static class UiCloning
{
    /// <summary>Copies an element next to the original (or under <paramref name="parent"/>).</summary>
    public static GameObject Clone(ITracker owner, Transform template, string name, Transform parent = null)
    {
        var go = Object.Instantiate(template.gameObject, parent != null ? parent : template.parent);
        go.name = name;
        go.SetActive(true);
        owner.Track(Disposable.Create(() => Remove(go)));
        return go;
    }

    /// <summary>
    /// Destroy only happens at the end of the frame, so a reloaded copy of a mod running in the
    /// same frame would still find the old object. Detach and hide it right away.
    /// </summary>
    public static void Remove(GameObject go)
    {
        if (go == null)
            return;
        go.SetActive(false);
        go.transform.SetParent(null, false);
        Object.Destroy(go);
    }

    /// <summary>
    /// Replaces every click handler on a button, including the ones saved in the scene, which
    /// RemoveAllListeners can't remove.
    /// </summary>
    public static Button SetClick(Button button, Action onClick)
    {
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() => onClick());
        return button;
    }

    /// <summary>Sets the first text found on or under the object (TextMeshPro or legacy Text).</summary>
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

    /// <summary>Hides a scene object and shows it again when the owner unloads.</summary>
    public static void Hide(ITracker owner, GameObject go)
    {
        if (go == null || !go.activeSelf)
            return;
        go.SetActive(false);
        owner.Track(Disposable.Create(() =>
        {
            if (go != null)
                go.SetActive(true);
        }));
    }
}
