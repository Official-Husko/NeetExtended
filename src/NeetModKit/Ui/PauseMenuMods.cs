using NeetLoader.Runtime;
using NeetModKit.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui;

/// <summary>
/// Replaces the pause menu's feedback column with a native-looking MODS column. The feedback form
/// posts to the developer's form, which makes no sense for a modded game, so it's hidden and the
/// space is used for the mod menu buttons instead. Built by copying the CONTROLS column.
/// </summary>
internal static class PauseMenuMods
{
    private const string ColumnName = "NeetModKit_Mods";
    private static readonly string[] FeedbackParts = { "ReportType", "CommentInput", "DataDisclaimer" };

    public static void Apply(ITracker owner)
    {
        var menu = Feedback_Sender._;
        var canvas = menu != null ? menu.transform.Find("Canvas_Settings") : null;
        if (canvas == null || canvas.Find(ColumnName) != null)
            return;

        var controls = canvas.Find("Controls");
        var feedbackHeader = canvas.Find("ReportType/Feedback_Text");
        if (controls == null || feedbackHeader == null)
            return;

        // Put the copied column's header exactly where the FEEDBACK header was.
        var column = UiCloning.Clone(owner, controls, ColumnName);
        var header = column.transform.Find("Display_Text (1)");
        var firstButton = column.transform.Find("ChangeControlsButton");
        if (header == null || firstButton == null)
            return;
        column.transform.position += feedbackHeader.position - header.position;
        UiCloning.SetText(header, "MODS");

        foreach (string part in FeedbackParts)
            UiCloning.Hide(owner, canvas.Find(part)?.gameObject);

        var second = UiCloning.Clone(owner, firstButton, "WhatsNewButton");
        Configure(firstButton, "MOD MENU", () => ModMenu.Open("Mods"));

        var rect = (RectTransform)firstButton;
        second.transform.localPosition = firstButton.localPosition - new Vector3(0f, rect.rect.height * 1.6f, 0f);
        Configure(second.transform, "WHAT'S NEW", () => ModMenu.Open(Menu.Pages.WhatsNewPage.PageTitle));
    }

    // The copied button is sized for "CHANGE"; our labels are longer.
    private const float WidthFactor = 2f;

    private static void Configure(Transform buttonObject, string label, System.Action onClick)
    {
        var rect = (RectTransform)buttonObject;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x * WidthFactor, rect.sizeDelta.y);
        UiCloning.SetText(buttonObject, label);
        var button = buttonObject.GetComponent<Button>();
        if (button != null)
            UiCloning.SetClick(button, () =>
            {
                Feedback_Sender._?.PlayMenuSFX(0);
                onClick();
            });
    }
}
