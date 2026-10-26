using System;
using System.Collections.Generic;
using CheatMenu.Pages;
using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Input;
using NeetModKit.Settings;
using NeetModKit.Ui.Toolkit;
using InputKey = UnityEngine.InputSystem.Key;

namespace CheatMenu;

/// <summary>A cheat window on its own key (Insert by default), built with the framework's UI toolkit.</summary>
public sealed class CheatMenuMod : NeetMod
{
    // Draw above the mod menu if both are open.
    private const int SortingOrder = 1100;

    private readonly List<UiPage> _pages = new List<UiPage> { new PlayerPage(), new ComputerPage(), new SpawnPage(), new WorldPage() };
    private readonly Hotkey _key = new Hotkey("Insert", InputKey.Insert);
    private ConfigFile<CheatMenuConfig> _config;
    private UiTabbedWindow _window;
    private IDisposable _inputLock;

    internal static CheatMenuMod Instance { get; private set; }

    public bool IsOpen { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        _config = LoadConfig<CheatMenuConfig>();
        _config.Save();
        _config.Changed += c => _key.Name = c.Key;
        _key.Name = _config.Value.Key;
        ModSettings.Register(this, _config, "Cheat Menu");
    }

    public override void OnUnload()
    {
        Close();
        Instance = null;
    }

    public override void OnUpdate()
    {
        if (_key.WasPressed || (IsOpen && Hotkey.EscapePressed))
            Toggle();
        if (IsOpen)
            _window.Tick();
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (IsOpen)
            return;
        if (_window == null)
        {
            if (!UiThemes.Menu.Ensure())
                return; // No menus loaded yet to build from.
            _window = new UiTabbedWindow(this, "CHEATS", () => _pages, Close, sortingOrder: SortingOrder);
        }
        IsOpen = true;
        _window.Open();
        _inputLock = UiInputLock.Acquire(_config.Value.PauseWhileOpen);
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        _window.Close();
        _inputLock?.Dispose();
        _inputLock = null;
    }

    /// <summary>For scripts and the dev bridge (type.call CheatMenu.CheatMenuMod OpenMenu).</summary>
    public static void OpenMenu() => Instance?.Open();

    public static void CloseMenu() => Instance?.Close();
}
