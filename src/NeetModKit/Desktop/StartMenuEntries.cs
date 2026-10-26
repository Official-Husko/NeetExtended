using NeetLoader;
using NeetModKit.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Desktop;

/// <summary>
/// Adds modded apps to the computer's start menu, copying the game's own entries. The menu grows
/// upward (it sits on the taskbar), with new entries at the bottom.
/// </summary>
internal static class StartMenuEntries
{
    private const string Template = "Btn_AnimeClicker";
    private const string Lowest = "Btn_Stocks";
    private const float Step = 13.8f;

    private static int _added;

    /// <summary>A new house means a fresh start menu.</summary>
    public static void Reset() => _added = 0;

    public static void Add(ComputerApp app, NeetMod owner)
    {
        var screen = ComputerControl.Screen;
        var menu = screen != null ? screen.Find("StartMenu") as RectTransform : null;
        var template = menu != null ? menu.Find(Template) : null;
        var lowest = menu != null ? menu.Find(Lowest) as RectTransform : null;
        if (template == null || lowest == null)
            return;

        MakeRoom(menu, owner);
        var entry = UiCloning.Clone(owner, template, "Btn_App_" + app.Id);
        var rect = (RectTransform)entry.transform;
        rect.anchoredPosition = new Vector2(lowest.anchoredPosition.x, lowest.anchoredPosition.y - Step * (_added + 1));
        UiCloning.SetText(entry.transform, string.IsNullOrEmpty(app.Definition.StartMenuLabel) ? app.Title : app.Definition.StartMenuLabel);

        // Same as the game's entries: open the browser, close the start menu, then go to the app.
        var command = entry.GetComponent<Computer_Button>();
        UiCloning.SetClick(entry.GetComponent<Button>(), () =>
        {
            command?.ButtonCommand();
            ComputerApps.Open(app);
        });
        ComputerControl.Main?.RegisterButtonClickAudio(entry.GetComponent<Button>());
        _added++;
    }

    /// <summary>Grows the menu by one row and lifts everything already in it.</summary>
    private static void MakeRoom(RectTransform menu, NeetLoader.Runtime.ITracker owner)
    {
        menu.sizeDelta += new Vector2(0f, Step);
        foreach (RectTransform child in menu)
            child.anchoredPosition += new Vector2(0f, Step / 2f);
        // New entries are placed relative to Btn_Stocks after this shift.
        owner.Track(NeetLoader.Runtime.Disposable.Create(() =>
        {
            if (menu == null)
                return;
            menu.sizeDelta -= new Vector2(0f, Step);
            foreach (RectTransform child in menu)
                child.anchoredPosition -= new Vector2(0f, Step / 2f);
        }));
    }
}
