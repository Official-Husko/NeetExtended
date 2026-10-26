using System;
using NeetLoader;
using NeetModKit.Registry;
// The game has its own global Key and Keyboard classes, so the Input System ones need aliases.
using InputKey = UnityEngine.InputSystem.Key;
using InputKeyboard = UnityEngine.InputSystem.Keyboard;

namespace NeetModKit.Menu;

/// <summary>
/// The in-game mod menu. Mods add their own tabs with <see cref="AddPage"/>; pages are removed
/// automatically when the mod unloads.
/// </summary>
public static class ModMenu
{
    private static readonly OwnedList<MenuPage> PageList = new OwnedList<MenuPage>();
    private static MenuWindow _window;
    private static InputKey _toggleKey = InputKey.F1;

    public static bool IsOpen { get; private set; }

    public static System.Collections.Generic.IReadOnlyList<MenuPage> Pages => PageList.Items;

    public static void AddPage(NeetMod owner, MenuPage page)
    {
        PageList.Add(owner, page);
        PageList.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Title, b.Title));
    }

    public static void Open(string pageTitle = null)
    {
        if (pageTitle != null)
            _window.Select(pageTitle);
        if (IsOpen)
            return;
        IsOpen = true;
        _window.OnOpened();
        MenuInput.Capture(KitMod.Config.Value.PauseWhileOpen);
    }

    public static void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        _window.OnClosed();
        MenuInput.Release();
    }

    public static void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    internal static void Init()
    {
        _window = new MenuWindow(PageList);
        ApplyConfig(KitMod.Config.Value);
        KitMod.Config.Changed += ApplyConfig;
    }

    internal static void Update()
    {
        var keyboard = InputKeyboard.current;
        if (keyboard == null)
            return;
        if (keyboard[_toggleKey].wasPressedThisFrame || (IsOpen && keyboard.escapeKey.wasPressedThisFrame))
            Toggle();
    }

    internal static void OnGUI()
    {
        if (IsOpen)
            _window.Draw();
    }

    private static void ApplyConfig(Settings.KitConfig config)
    {
        _toggleKey = Enum.TryParse(config.MenuKey, true, out InputKey key) && key != InputKey.None ? key : InputKey.F1;
    }
}
