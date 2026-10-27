using System;
using System.Collections.Generic;
using NeetLoader;
using NeetModKit.Input;
using NeetModKit.Registry;
using NeetModKit.Ui.Toolkit;
using InputKey = UnityEngine.InputSystem.Key;

namespace NeetModKit.Menu;

/// <summary>
/// The in-game mod menu, built from the game's own menu elements. A few tabs along the top (Mods,
/// Mod Settings, What's New, Errors, and any a mod adds with <see cref="AddTab"/>); the pages mods
/// add with <see cref="AddPage"/> are listed per mod on the Mod Settings tab, next to their
/// settings. Pages are removed automatically when the mod unloads.
/// </summary>
public static class ModMenu
{
    private const string ReopenKey = "NeetModKit.MenuOpenOn";

    /// <summary>A page a mod added, listed under that mod on the Mod Settings tab.</summary>
    internal sealed class ModPage
    {
        public NeetMod Owner;
        public MenuPage Page;
    }

    private static readonly OwnedList<MenuPage> TabList = new OwnedList<MenuPage>();
    private static readonly OwnedList<ModPage> ModPageList = new OwnedList<ModPage>();
    private static readonly Hotkey Key = new Hotkey("F1", InputKey.F1);
    private static UiTabbedWindow _window;
    private static IDisposable _inputLock;
    private static NeetMod _owner;

    public static bool IsOpen { get; private set; }

    /// <summary>The tabs along the top.</summary>
    public static IReadOnlyList<MenuPage> Pages => TabList.Items;

    internal static IReadOnlyList<ModPage> ModPages => ModPageList.Items;

    /// <summary>Adds a page for <paramref name="owner"/> to the Mod Settings tab, under the mod's name.</summary>
    public static void AddPage(NeetMod owner, MenuPage page)
    {
        ModPageList.Add(owner, new ModPage { Owner = owner, Page = page });
        ModPageList.Sort((a, b) => a.Page.Order != b.Page.Order ? a.Page.Order.CompareTo(b.Page.Order) : string.CompareOrdinal(a.Page.Title, b.Page.Title));
    }

    /// <summary>Adds a tab along the top. Keep these few: most pages belong on Mod Settings (<see cref="AddPage"/>).</summary>
    public static void AddTab(NeetMod owner, MenuPage page)
    {
        TabList.Add(owner, page);
        TabList.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Title, b.Title));
    }

    public static void Open(string pageTitle = null)
    {
        // The window is built from the pause menu's elements, which only exist once a scene is up.
        if (_window == null)
        {
            if (!UiThemes.Menu.Ensure())
            {
                KitMod.Instance?.Log.Warn("The mod menu can't open before the game's menus are loaded.");
                return;
            }
            _window = new UiTabbedWindow(_owner, About.PackInfo.Title, () => TabList.Items, Close);
        }

        if (pageTitle != null && !_window.Select(pageTitle) && NeetModKit.Menu.Pages.ModSettingsPage.Show(pageTitle))
            _window.Select(NeetModKit.Menu.Pages.ModSettingsPage.PageTitle);
        if (IsOpen)
            return;
        IsOpen = true;
        _window.Open();
        _inputLock = UiInputLock.Acquire(KitMod.Config.Value.PauseWhileOpen);
    }

    public static void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        _window.Close();
        _inputLock?.Dispose();
        _inputLock = null;
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
        TabList.Changed += () => _window?.PagesChanged();
        ModPageList.Changed += NeetModKit.Menu.Pages.ModSettingsPage.PagesChanged;
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
        UiInputLock.ReleaseAll();
        UiThemes.ReleaseAll();
        _window = null;
    }

    /// <summary>After a reload, reopen the menu where the player left it.</summary>
    // On the next frame: during OnLoad the framework doesn't count as loaded yet (so What's New
    // found no changelog), and the mods that add their own tabs haven't loaded again.
    internal static void ReopenIfReloaded()
    {
        if (NeetLoader.Runtime.ReloadState.Take(ReopenKey, out string page))
            NeetLoader.Runtime.MainThread.Post(() => Open(page));
    }

    internal static void Update()
    {
        if (Key.WasPressed || (IsOpen && Hotkey.EscapePressed))
            Toggle();
        if (IsOpen)
            _window.Tick();
    }

    private static void ApplyConfig(Settings.KitConfig config) => Key.Name = config.MenuKey;
}
