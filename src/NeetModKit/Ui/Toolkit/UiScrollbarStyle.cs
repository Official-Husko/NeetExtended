using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui.Toolkit;

/// <summary>
/// Colours for scrollbars built in a theme. The track and the handle both use the handle's own
/// sprite (the game's rounded "UISprite"), the same as its buttons and slider fills.
/// </summary>
public sealed class UiScrollbarStyle
{
    public Color Track = new Color(0.06f, 0.02f, 0.14f, 0.6f);
    public Color Handle = new Color(1f, 0.2475f, 0f, 1f);
    public Color HandleHover = new Color(1f, 0.45f, 0.22f, 1f);
    public Color HandlePressed = new Color(0.2906f, 0f, 1f, 1f);

    /// <summary>The pause menu's colours: orange like its sliders and the selected tab, purple while dragged.</summary>
    public static readonly UiScrollbarStyle Menu = new UiScrollbarStyle();

    public void Apply(Scrollbar scrollbar)
    {
        var handle = scrollbar.handleRect != null ? scrollbar.handleRect.GetComponent<Image>() : null;
        var track = scrollbar.GetComponent<Image>();
        if (handle == null)
            return;

        if (track != null)
        {
            track.sprite = handle.sprite;
            track.type = Image.Type.Sliced;
            track.color = Track;
        }

        // The handle stays white so the colour block below is the colour it shows.
        handle.color = Color.white;
        var colors = scrollbar.colors;
        colors.normalColor = Handle;
        colors.highlightedColor = HandleHover;
        colors.selectedColor = Handle;
        colors.pressedColor = HandlePressed;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        scrollbar.colors = colors;
        scrollbar.transition = Selectable.Transition.ColorTint;
        scrollbar.targetGraphic = handle;
    }
}
