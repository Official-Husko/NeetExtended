using System;
using System.Collections.Generic;
using HarmonyLib;
using NeetModKit.Game;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NeetModKit.Desktop;

/// <summary>
/// The in-game computer: power, the browser and its "websites". Sites are panels the browser
/// switches between by URL (anime, pun, stonk...).
/// </summary>
public static class ComputerControl
{
    private static readonly AccessTools.FieldRef<Computer, float> ScreensaverTimer =
        AccessTools.FieldRefAccess<Computer, float>("timerToScreenSaver");

    public static Computer Main => GameRefs.Computer;

    /// <summary>The world-space canvas the computer draws on.</summary>
    public static Transform Screen => Main != null ? Main.transform : null;

    public static Computer_Browser_Addresser Browser =>
        Screen != null ? Screen.GetComponentInChildren<Computer_Browser_Addresser>(true) : null;

    public static IReadOnlyList<string> SiteUrls => Browser != null ? Browser.urls : (IReadOnlyList<string>)Array.Empty<string>();

    /// <summary>Turns the computer on, skipping the boot animation.</summary>
    public static void PowerOn()
    {
        var button = Object.FindObjectOfType<Computer_PowerButton>(true)
            ?? throw new InvalidOperationException("No computer power button (not in the house?).");
        button.StartComputer(instant: true);
        WakeScreen();
    }

    /// <summary>Hides the screensaver and restarts its 30 second countdown.</summary>
    public static void WakeScreen()
    {
        var computer = Main;
        if (computer == null)
            return;
        ScreensaverTimer(computer) = 0f;
        if (computer.screenSaver != null)
            computer.screenSaver.SetActive(false);
    }

    /// <summary>Opens the browser on a site by its URL name, e.g. "anime".</summary>
    public static void OpenSite(string url)
    {
        var browser = Browser ?? throw new InvalidOperationException("The computer isn't loaded.");
        if (!browser.urls.Contains(url))
            throw new ArgumentException($"No site '{url}'. Sites: {string.Join(", ", browser.urls)}");

        // The browser window is the panel holding the address bar; make sure it's showing.
        for (var t = browser.transform; t != null && t != Screen; t = t.parent)
            t.gameObject.SetActive(true);
        browser.LoadWebsite(url);
        WakeScreen();
    }

    /// <summary>The site panel for a URL, e.g. to add buttons to it.</summary>
    public static GameObject Site(string url)
    {
        var browser = Browser;
        if (browser == null)
            return null;
        int index = browser.urls.IndexOf(url);
        return index >= 0 && index < browser.websites.Length ? browser.websites[index] : null;
    }

    /// <summary>Puts the player in front of the monitor, looking at the screen. Handy for testing.</summary>
    public static void StandInFront(float distance = 0.7f)
    {
        var screen = Screen ?? throw new InvalidOperationException("The computer isn't loaded.");
        var player = GameRefs.Player ?? throw new InvalidOperationException("No player right now.");

        // The canvas is seen from behind its forward axis; keep the player's current floor height.
        Vector3 facing = Vector3.ProjectOnPlane(screen.forward, Vector3.up).normalized;
        Vector3 spot = screen.position - facing * distance;
        spot.y = player.transform.position.y;
        PlayerControl.Teleport(spot);

        var eye = Camera.main != null ? Camera.main.transform.position : spot + Vector3.up * 0.53f;
        PlayerControl.Look(screen.position - eye);
    }
}
