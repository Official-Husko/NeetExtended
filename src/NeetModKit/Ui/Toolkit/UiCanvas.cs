using NeetLoader;
using NeetLoader.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui.Toolkit;

/// <summary>Screen overlay canvases that scale like the game's own menus.</summary>
public static class UiCanvas
{
    /// <summary>
    /// Creates a persistent overlay canvas owned by a mod. Higher sorting orders draw on top.
    /// <paramref name="interactive"/> false leaves out the raycaster, so the canvas never takes
    /// clicks. A <paramref name="referenceResolution"/> scales it on its own instead of like the
    /// game's menus.
    /// </summary>
    public static Canvas Create(ITracker owner, string name, int sortingOrder = 1000, bool interactive = true,
        Vector2? referenceResolution = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Object.DontDestroyOnLoad(go);
        owner.Track(Disposable.Destroying(go));

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.AddComponent<CanvasScaler>();
        var source = UiThemes.MenuScaler;
        if (referenceResolution.HasValue)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution.Value;
            scaler.matchWidthOrHeight = 0.5f;
        }
        else if (source != null)
        {
            scaler.uiScaleMode = source.uiScaleMode;
            scaler.referenceResolution = source.referenceResolution;
            scaler.screenMatchMode = source.screenMatchMode;
            scaler.matchWidthOrHeight = source.matchWidthOrHeight;
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1148.7f, 436.8f);
        }

        if (interactive)
            go.AddComponent<GraphicRaycaster>();
        return canvas;
    }
}
