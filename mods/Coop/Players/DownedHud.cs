using NeetModKit.Ui.Toolkit;
using UnityEngine;
using UnityEngine.UI;

namespace Coop.Players;

/// <summary>
/// The line across the screen while you're down, counting the seconds you have left, and the bar
/// under the crosshair while you pump someone else's chest.
/// </summary>
internal static class DownedHud
{
    private static Canvas _canvas;
    private static Text _text;
    private static Text _pumping;
    private static int _shownSeconds = -1;
    private static int _shownPump = -1;
    private static bool _shownCarried;

    public static void Show(float secondsLeft)
    {
        int seconds = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
        bool carried = Carrying.LocalCarried;
        if (_text == null)
            Build();
        if (seconds == _shownSeconds && carried == _shownCarried && _text.enabled)
            return;
        _shownSeconds = seconds;
        _shownCarried = carried;
        _text.enabled = true;
        _canvas.enabled = true;
        _text.text = $"<size=150%><color=red>U R DOWN</color></size>\nget fed, get a drink or get ur chest pumped or ur dead in {seconds}"
            + (carried ? "\n<size=70%>somebody is hauling ur fat ass around</size>" : "");
    }

    public static void Hide()
    {
        _shownSeconds = -1;
        if (_text != null)
            _text.enabled = false;
        UpdateCanvas();
    }

    /// <summary>Pumping someone's chest: <paramref name="progress"/> 0 to 1.</summary>
    public static void ShowPumping(float progress)
    {
        int percent = Mathf.Clamp(Mathf.RoundToInt(progress * 10f), 0, 10);
        if (_pumping == null)
            Build();
        if (percent == _shownPump && _pumping.enabled)
            return;
        _shownPump = percent;
        _pumping.enabled = true;
        _canvas.enabled = true;
        _pumping.text = "<size=70%>pumping their fat chest</size>\n" + new string('|', percent) + "<color=#666666>" + new string('|', 10 - percent) + "</color>";
    }

    public static void HidePumping()
    {
        _shownPump = -1;
        if (_pumping != null)
            _pumping.enabled = false;
        UpdateCanvas();
    }

    // One canvas for both lines: on while either shows.
    private static void UpdateCanvas()
    {
        if (_canvas != null)
            _canvas.enabled = (_text != null && _text.enabled) || (_pumping != null && _pumping.enabled);
    }

    private static void Build()
    {
        _canvas = UiCanvas.Create(CoopMod.Instance, "CoopDowned", sortingOrder: 900, interactive: false,
            referenceResolution: new Vector2(1600f, 900f));
        _text = AddText("Text", new Vector2(0f, 0.55f), new Vector2(1f, 0.75f), 36);
        _pumping = AddText("Pumping", new Vector2(0f, 0.25f), new Vector2(1f, 0.4f), 30);
    }

    private static Text AddText(string name, Vector2 min, Vector2 max, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_canvas.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.enabled = false;
        go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        return text;
    }
}
