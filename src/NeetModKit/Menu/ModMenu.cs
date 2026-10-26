using System;
using NeetLoader;
using NeetModKit.Registry;
using NeetModKit.Ui.Toolkit;
// The game has its own global Key and Keyboard classes, so the Input System ones need aliases.
using InputKey = UnityEngine.InputSystem.Key;
using InputKeyboard = UnityEngine.InputSystem.Keyboard;

namespace NeetModKit.Menu;

/// <summary>
/// The in-game mod menu, built from the game's own menu elements. Mods add tabs with
/// <see cref="AddPage"/>; pages are removed automatically when the mod unloads.
/// </summary>
public static class ModMenu
{
    private const string ReopenKey = "NeetModKit.MenuOpenOn";

    private static readonly OwnedList<MenuPage> PageList = new OwnedList<MenuPage>();
    private static MenuWindow _window;
    private static NeetMod _owner;
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
        // The window is built from the pause menu's elements, which only exist once a scene is up.
        if (_window == null)
        {
            if (!UiTemplates.Ensure())
            {
                KitMod.Instance?.Log.Warn("The mod menu can't open before the game's menus are loaded.");
                return;
            }
            _window = new MenuWindow(_owner, PageList);
        }

        if (pageTitle != null)
            _window.Select(pageTitle);
        if (IsOpen)
            return;
        IsOpen = true;
        _window.Open();
        MenuInput.Capture(KitMod.Config.Value.PauseWhileOpen);
    }

    public static void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        _window.Close();
        MenuInput.Release();
    }

    public static void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    internal static void Init(NeetMod owner)
    {
        _owner = owner;
        ApplyConfig(KitMod.Config.Value);
        KitMod.Config.Changed += ApplyConfig;
    }

    /// <summary>Closes the menu for a framework unload, remembering the page so a reload can reopen it.</summary>
    internal static void Shutdown()
    {
        if (IsOpen)
            NeetLoader.Runtime.ReloadState.Put(ReopenKey, _window.SelectedTitle);
        Close();
        KitMod.Config.Changed -= ApplyConfig;
        UiTemplates.Release();
        _window = null;
    }

    /// <summary>After a reload, reopen the menu where the player left it.</summary>
    internal static void ReopenIfReloaded()
    {
        if (NeetLoader.Runtime.ReloadState.Take(ReopenKey, out string page))
            Open(page);
    }

    internal static void Update()
    {
        var keyboard = InputKeyboard.current;
        if (keyboard != null && (keyboard[_toggleKey].wasPressedThisFrame || (IsOpen && keyboard.escapeKey.wasPressedThisFrame)))
            Toggle();
        if (IsOpen)
            _window.Tick();
    }

    private static void ApplyConfig(Settings.KitConfig config)
    {
        _toggleKey = Enum.TryParse(config.MenuKey, true, out InputKey key) && key != InputKey.None ? key : InputKey.F1;
    }
}
