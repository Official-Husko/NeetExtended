using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui.Toolkit;

public sealed partial class UiBuilder
{
    private const float ScrollbarWidth = 8f;

    /// <summary>
    /// A vertically scrolling area that takes the remaining height. Returns a builder for its
    /// content. Uses the scrollbar from the game's dropdowns.
    /// </summary>
    public UiBuilder Scroll(out ScrollRect scrollRect, float flexibleHeight = 1f, float spacing = 6f)
    {
        var area = NewChild("Scroll");
        Size(area, flexibleWidth: 1f, flexibleHeight: flexibleHeight);

        var viewport = Stretch(new UiBuilder(area).NewChild("Viewport"), right: ScrollbarWidth + 4f);
        viewport.gameObject.AddComponent<RectMask2D>();
        // An invisible graphic so the mouse wheel works anywhere over the area, not only over text.
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

        var content = new UiBuilder(viewport).NewChild("Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect = area.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 25f;

        var bar = Object.Instantiate(UiTemplates.Get(UiTemplate.Scrollbar), area, false);
        bar.name = "Scrollbar";
        var barRect = (RectTransform)bar.transform;
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.offsetMin = new Vector2(-ScrollbarWidth, 0f);
        barRect.offsetMax = Vector2.zero;
        var scrollbar = bar.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        return Vertical(content, spacing);
    }

    /// <summary>Fills the parent, minus margins.</summary>
    internal static RectTransform Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }
}
