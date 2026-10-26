using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NeetLoader;
using NeetLoader.Config;
using NeetLoader.Logging;
using NeetLoader.Runtime;
using NeetModKit.Events;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Desktop;

/// <summary>
/// Every app on the in-game computer: the game's own websites (adopted when the house loads) and
/// apps added by mods. The browser is routed through here, so every app has a URL the player can
/// change; the URL an app shipped with keeps working as an alias, which is what the game's own
/// start menu buttons use.
/// </summary>
public static class ComputerApps
{
    private static readonly Regex ValidUrl = new Regex("^[a-z0-9-]{1,32}$");
    private static readonly List<ComputerApp> Apps = new List<ComputerApp>();
    private static Dictionary<string, string> _urls = new Dictionary<string, string>();
    private static string _urlsPath;
    private static ModLogger _log;
    private static Computer_Browser_Addresser _adoptedFrom;

    public static IReadOnlyList<ComputerApp> All => Apps;

    public static ComputerApp Find(string id) => Apps.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Adds an app or game. Its page is built whenever the house's computer is set up.</summary>
    public static ComputerApp Register(NeetMod owner, ComputerAppDefinition def)
    {
        if (string.IsNullOrEmpty(def.Id) || def.Build == null)
            throw new ArgumentException("An app needs an Id and a Build callback.");
        if (Find(def.Id) != null)
            throw new ArgumentException($"An app with id '{def.Id}' already exists.");

        var app = new ComputerApp(def.Id, def.Title ?? def.Id, def.Description, Normalize(def.Url ?? def.Id), owner, def);
        Apps.Add(app);
        owner.Track(Disposable.Create(() =>
        {
            Apps.Remove(app);
            if (app.Panel != null)
                UnityEngine.Object.Destroy(app.Panel);
            app.Panel = null;
        }));

        if (GameEvents.IsHouseReady && Browser != null)
            Install(app, owner);
        return app;
    }

    public static string UrlOf(ComputerApp app) => _urls.TryGetValue(app.Id, out string url) ? url : app.DefaultUrl;

    /// <summary>Changes an app's URL. Returns null on success, otherwise why it was refused.</summary>
    public static string SetUrl(ComputerApp app, string url)
    {
        url = Normalize(url);
        if (!ValidUrl.IsMatch(url))
            return "Use 1-32 letters, digits or dashes.";
        var clash = Apps.FirstOrDefault(a => a != app && (UrlOf(a) == url || a.DefaultUrl == url));
        if (clash != null)
            return $"{clash.Title} already uses \"{url}\".";

        if (url == app.DefaultUrl)
            _urls.Remove(app.Id);
        else
            _urls[app.Id] = url;
        SaveUrls();
        return null;
    }

    /// <summary>Opens the browser on an app (the computer must be on to see it).</summary>
    public static void Open(ComputerApp app)
    {
        var browser = Browser ?? throw new InvalidOperationException("The computer isn't loaded.");
        for (var t = browser.transform; t != null && t != ComputerControl.Screen; t = t.parent)
            t.gameObject.SetActive(true);
        browser.LoadWebsite(UrlOf(app));
    }

    /// <summary>
    /// Shows the app matching what was typed in the address bar. Matches the current URL first,
    /// then the original URL (alias), then the app id. Returns the app shown, or null.
    /// </summary>
    internal static ComputerApp Navigate(Computer_Browser_Addresser browser)
    {
        EnsureAdopted(browser);
        var field = browser.GetComponent<InputField>();
        string typed = Normalize(field != null ? field.text : "");

        var app = Apps.FirstOrDefault(a => UrlOf(a) == typed)
                  ?? Apps.FirstOrDefault(a => a.DefaultUrl == typed)
                  ?? Find(typed);

        foreach (var other in Apps)
        {
            if (other.Panel != null)
                other.Panel.SetActive(false);
        }
        if (app?.Panel != null)
            app.Panel.SetActive(true);

        if (field != null)
        {
            field.text = "http://" + (app != null ? UrlOf(app) : typed) + ".com";
            field.MoveTextEnd(false);
        }
        return app;
    }

    internal static void Init(NeetMod kit)
    {
        _log = kit.Log;
        _urlsPath = kit.PathTo("app-urls.json");
        _urls = File.Exists(_urlsPath) ? JsonFile.Read<Dictionary<string, string>>(_urlsPath) : new Dictionary<string, string>();
        GameEvents.WhenHouseReady(kit, () => SetUpHouse(kit));
    }

    internal static Computer_Browser_Addresser Browser => ComputerControl.Browser;

    /// <summary>Adopts the game's websites and builds every modded app on this house's computer.</summary>
    private static void SetUpHouse(NeetMod kit)
    {
        var browser = Browser;
        if (browser == null)
            return;

        EnsureAdopted(browser);
        StartMenuEntries.Reset();
        foreach (var app in Apps.Where(a => !a.BuiltIn).ToList())
            Install(app, app.Owner);
    }

    /// <summary>The game can navigate before the house is ready, so adopt on first use too.</summary>
    private static void EnsureAdopted(Computer_Browser_Addresser browser)
    {
        if (_adoptedFrom == browser)
            return;
        AdoptBuiltIns(browser);
        _adoptedFrom = browser;
    }

    private static void AdoptBuiltIns(Computer_Browser_Addresser browser)
    {
        int count = Math.Min(browser.urls.Count, browser.websites.Length);
        for (int i = 0; i < count; i++)
        {
            var panel = browser.websites[i];
            if (panel == null)
                continue;
            var app = Apps.FirstOrDefault(a => a.BuiltIn && a.Id == panel.name);
            if (app == null)
            {
                var (title, description) = BuiltInApps.Describe(panel.name);
                app = new ComputerApp(panel.name, title, description, browser.urls[i], null, null);
                Apps.Insert(Apps.Count(a => a.BuiltIn), app);
            }
            app.Panel = panel;
        }
    }

    private static void Install(ComputerApp app, NeetMod owner)
    {
        try
        {
            app.Panel = AppPanels.Create(app, owner);
            if (app.Definition.InStartMenu)
                StartMenuEntries.Add(app, owner);
        }
        catch (Exception e)
        {
            _log.Error($"Couldn't set up app '{app.Id}' from {owner.Info.Id}", e);
        }
    }

    /// <summary>"HTTP://www.Anime.com/" -> "anime".</summary>
    internal static string Normalize(string text)
    {
        text = (text ?? "").Trim().ToLowerInvariant();
        foreach (string prefix in new[] { "http://", "https://", "www." })
        {
            if (text.StartsWith(prefix))
                text = text.Substring(prefix.Length);
        }
        text = text.TrimEnd('/');
        if (text.EndsWith(".com"))
            text = text.Substring(0, text.Length - 4);
        return text;
    }

    private static void SaveUrls()
    {
        try { JsonFile.Write(_urlsPath, _urls); }
        catch (Exception e) { _log.Error("Couldn't save app URLs", e); }
    }
}
