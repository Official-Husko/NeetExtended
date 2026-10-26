using NeetLoader.Runtime;
using NeetModKit.About;
using NeetModKit.Ui.Toolkit;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui;

/// <summary>
/// The pack's name, version and commit in the bottom right corner of every screen, so players can
/// see the mods are running and screenshots show which build they came from. Doesn't take clicks.
/// </summary>
internal static class VersionTag
{
    private const int FontSize = 15;
    private static readonly Color Colour = new Color(0.55f, 0.55f, 0.55f, 0.7f);
    private static readonly Vector2 Margin = new Vector2(10f, 6f);

    private static GameObject _root;

    public static void Create(ITracker owner, bool visible)
    {
        // Under the mod menu's windows (1000), over the game's own screens.
        var canvas = UiCanvas.Create(owner, "NeetModKit.VersionTag", sortingOrder: 900, interactive: false,
            referenceResolution: new Vector2(1920f, 1080f));
        _root = canvas.gameObject;

        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
        label.transform.SetParent(canvas.transform, false);
        label.text = PackInfo.Label;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = FontSize;
        label.color = Colour;
        label.alignment = TextAnchor.LowerRight;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.raycastTarget = false;

        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-Margin.x, Margin.y);
        rect.sizeDelta = new Vector2(600f, FontSize * 2f);

        SetVisible(visible);
    }

    public static void SetVisible(bool visible)
    {
        if (_root != null && _root.activeSelf != visible)
            _root.SetActive(visible);
    }
}
