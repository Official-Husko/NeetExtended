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

    /// <summary>Current URL of every app.</summary>
    public static List<string> SiteUrls
    {
        get
        {
            var urls = new List<string>();
            foreach (var app in ComputerApps.All)
                urls.Add(app.Url);
            return urls;
        }
    }

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

    /// <summary>Opens the browser on an app by URL (current or original) or id, e.g. "anime".</summary>
    public static void OpenSite(string urlOrId)
    {
        string key = ComputerApps.Normalize(urlOrId);
        ComputerApp app = null;
        foreach (var candidate in ComputerApps.All)
        {
            if (candidate.Url == key || candidate.DefaultUrl == key || string.Equals(candidate.Id, urlOrId, StringComparison.OrdinalIgnoreCase))
            {
                app = candidate;
                break;
            }
        }
        if (app == null)
            throw new ArgumentException($"No app '{urlOrId}'. Apps: {string.Join(", ", SiteUrls)}");
        ComputerApps.Open(app);
        WakeScreen();
    }

    /// <summary>The page of an app by id; null outside the house.</summary>
    public static GameObject Site(string id) => ComputerApps.Find(id)?.Panel;

    /// <summary>Puts the player in front of the monitor, looking at the screen. Handy for testing.</summary>
    public static void StandInFront(float distance = 0.7f)
    {
        var screen = Screen ?? throw new InvalidOperationException("The computer isn't loaded.");
        var player = GameRefs.Player ?? throw new InvalidOperationException("No player right now.");

        // The canvas is seen from behind its forward axis. Stand on the floor of the computer's room,
        // wherever in the house the player is now.
        Vector3 facing = Vector3.ProjectOnPlane(screen.forward, Vector3.up).normalized;
        Vector3 spot = screen.position - facing * distance;
        var room = Rooms.At(screen.position);
        spot.y = Rooms.Exists(room) ? Locator.GetFlooredPositionInRoom(room, spot).y + PlayerControl.StandingHeight : player.transform.position.y;
        PlayerControl.Teleport(spot);

        var eye = Camera.main != null ? Camera.main.transform.position : spot + Vector3.up * 0.53f;
        PlayerControl.Look(screen.position - eye);
    }
}
