using NeetLoader;
using NeetLoader.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui.Toolkit;

/// <summary>Screen overlay canvases that scale like the game's own menus.</summary>
public static class UiCanvas
{
    /// <summary>Creates a persistent overlay canvas owned by a mod. Higher sorting orders draw on top.</summary>
    public static Canvas Create(ITracker owner, string name, int sortingOrder = 1000)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Object.DontDestroyOnLoad(go);
        owner.Track(Disposable.Destroying(go));

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.AddComponent<CanvasScaler>();
        var source = UiThemes.MenuScaler;
        if (source != null)
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

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }
}
