using NeetModKit.Menu;
using NeetModKit.Ui.Toolkit;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui.Notifications;

/// <summary>
/// One notice on screen, built from the pause menu's pieces: its dark backdrop, an orange stripe,
/// the orange display font for the title, the menu's small text for the detail, and a bar in the
/// menu's violet and orange. Falls back to plain text if the menu isn't loaded yet.
/// </summary>
internal sealed class NoticeView
{
    private const int TitleSize = 22;
    private const int DetailSize = 15;
    private const float BarHeight = 8f;
    private static readonly Color Panel = new Color(0.1f, 0.04f, 0.17f, 0.92f);
    private static readonly Color BarBack = new Color(0.32f, 0.16f, 0.72f, 0.6f);

    private readonly GameObject _root;
    private readonly CanvasGroup _group;
    private readonly Text _title;
    private readonly Text _detail;
    private readonly GameObject _bar;
    private readonly RectTransform _fill;

    public NoticeView(Notice notice, RectTransform parent, float width)
    {
        Notice = notice;
        ShownAt = Time.unscaledTime;
        bool themed = UiThemes.Menu.Ensure();

        _root = new GameObject("Notice", typeof(RectTransform));
        _root.transform.SetParent(parent, false);
        _group = _root.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var background = _root.AddComponent<Image>();
        CopyLook(themed ? UiThemes.Menu.Get(UiTemplate.Backdrop) : null, background);
        background.color = Panel;
        background.raycastTarget = false;

        var layout = _root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 12, 9, 10);
        layout.spacing = 3f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        UiBuilder.Size(_root.transform, width: width);

        AddStripe();
        _title = AddText(themed ? UiTemplate.Header : (UiTemplate?)null, "Title", TitleSize, MenuPalette.Accent);
        _detail = AddText(themed ? UiTemplate.Text : (UiTemplate?)null, "Detail", DetailSize, MenuPalette.Muted);
        (_bar, _fill) = AddBar();

        _root.transform.SetAsLastSibling();
        Refresh();
    }

    public Notice Notice { get; }

    public float ShownAt { get; }

    public float Alpha
    {
        set
        {
            if (Mathf.Abs(_group.alpha - value) > 0.003f)
                _group.alpha = value;
        }
    }

    public void Refresh()
    {
        _title.text = Notice.Title;
        _detail.text = Notice.Detail;
        _detail.gameObject.SetActive(Notice.Detail.Length > 0);
        bool showBar = Notice.Progress.HasValue;
        if (_bar.activeSelf != showBar)
            _bar.SetActive(showBar);
        if (showBar)
            _fill.anchorMax = new Vector2(Notice.Progress.Value, 1f);
    }

    public void Destroy()
    {
        if (_root != null)
            Object.Destroy(_root);
    }

    // A thin orange edge on the left, outside the layout.
    private void AddStripe()
    {
        var go = new GameObject("Stripe", typeof(RectTransform));
        go.transform.SetParent(_root.transform, false);
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(6f, 0f);
        rect.anchoredPosition = Vector2.zero;
        var image = go.AddComponent<Image>();
        image.color = MenuPalette.Accent;
        image.raycastTarget = false;
    }

    private Text AddText(UiTemplate? template, string name, int size, Color color)
    {
        Text text;
        if (template.HasValue)
        {
            var go = Object.Instantiate(UiThemes.Menu.Get(template.Value), _root.transform, false);
            go.name = name;
            go.SetActive(true);
            // The template's own sizing would fight the notice's layout.
            foreach (var fitter in go.GetComponents<ContentSizeFitter>())
                Object.DestroyImmediate(fitter);
            text = go.GetComponent<Text>();
        }
        else
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);
            text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;
        text.raycastTarget = false;
        var element = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
        element.minHeight = -1f;
        element.preferredHeight = -1f;
        element.flexibleHeight = -1f;
        return text;
    }

    private (GameObject, RectTransform) AddBar()
    {
        var bar = new GameObject("Bar", typeof(RectTransform));
        bar.transform.SetParent(_root.transform, false);
        var back = bar.AddComponent<Image>();
        back.color = BarBack;
        back.raycastTarget = false;
        var element = bar.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = BarHeight;

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(bar.transform, false);
        var fill = (RectTransform)fillGo.transform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        var image = fillGo.AddComponent<Image>();
        image.color = MenuPalette.Accent;
        image.raycastTarget = false;
        return (bar, fill);
    }

    // The backdrop's sprite (if it has one), so the panel has the menu's texture.
    private static void CopyLook(GameObject template, Image target)
    {
        var source = template != null ? template.GetComponent<Image>() : null;
        if (source == null || source.sprite == null)
            return;
        target.sprite = source.sprite;
        target.type = source.type;
    }
}
