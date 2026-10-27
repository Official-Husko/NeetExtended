using NeetModKit.Ui.Toolkit;
using UnityEngine;
using UnityEngine.UI;

namespace Coop.Players;

/// <summary>The line across the screen while you're down, counting the seconds you have left.</summary>
internal static class DownedHud
{
    private static Canvas _canvas;
    private static Text _text;
    private static int _shownSeconds = -1;

    public static void Show(float secondsLeft)
    {
        int seconds = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
        if (_text == null)
            Build();
        if (seconds == _shownSeconds && _canvas.enabled)
            return;
        _shownSeconds = seconds;
        _canvas.enabled = true;
        _text.text = $"<size=150%><color=red>U R DOWN</color></size>\nsomeone shove food in ur face or ur dead in {seconds}";
    }

    public static void Hide()
    {
        _shownSeconds = -1;
        if (_canvas != null)
            _canvas.enabled = false;
    }

    private static void Build()
    {
        _canvas = UiCanvas.Create(CoopMod.Instance, "CoopDowned", sortingOrder: 900, interactive: false,
            referenceResolution: new Vector2(1600f, 900f));
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(_canvas.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 0.55f);
        rect.anchorMax = new Vector2(1f, 0.75f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        _text = go.AddComponent<Text>();
        _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _text.fontSize = 36;
        _text.alignment = TextAnchor.MiddleCenter;
        _text.color = Color.white;
        _text.supportRichText = true;
        _text.horizontalOverflow = HorizontalWrapMode.Wrap;
        _text.verticalOverflow = VerticalWrapMode.Overflow;
        go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
    }
}
