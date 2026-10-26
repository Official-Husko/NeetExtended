using System;
using NeetLoader;
using NeetModKit.Ui.Toolkit;
using UnityEngine;

namespace NeetModKit.Desktop;

/// <summary>A website/app on the in-game computer: one of the game's own, or added by a mod.</summary>
public sealed class ComputerApp
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }

    /// <summary>The URL the app shipped with. It keeps working as an alias after the player changes the URL.</summary>
    public string DefaultUrl { get; }

    /// <summary>The URL in use (the player's choice or the default).</summary>
    public string Url => ComputerApps.UrlOf(this);

    public bool BuiltIn => Owner == null;

    /// <summary>The mod that added the app; null for the game's own.</summary>
    public NeetMod Owner { get; }

    /// <summary>The app's page on the computer; null outside the house.</summary>
    public GameObject Panel { get; internal set; }

    internal ComputerAppDefinition Definition { get; }

    internal ComputerApp(string id, string title, string description, string defaultUrl, NeetMod owner, ComputerAppDefinition definition)
    {
        Id = id;
        Title = title;
        Description = description;
        DefaultUrl = defaultUrl;
        Owner = owner;
        Definition = definition;
    }
}

/// <summary>What a mod provides to add an app or game to the computer.</summary>
public sealed class ComputerAppDefinition
{
    /// <summary>Stable id, unique across mods (it keys the player's URL choice).</summary>
    public string Id;
    public string Title;
    public string Description;

    /// <summary>Address without "http://" and ".com", e.g. "pumpkin" for http://pumpkin.com.</summary>
    public string Url;

    /// <summary>Page height in computer units (the screen shows about 120 at a time; taller pages scroll).</summary>
    public float Height = 120f;

    /// <summary>Add an entry to the computer's start menu.</summary>
    public bool InStartMenu = true;

    /// <summary>Start menu text; the title if empty.</summary>
    public string StartMenuLabel;

    /// <summary>Builds the page. Runs every time the house's computer is set up (each house load).</summary>
    public Action<ComputerAppContext> Build;
}

/// <summary>Everything an app's Build callback needs.</summary>
public sealed class ComputerAppContext
{
    public ComputerApp App { get; }

    /// <summary>The page itself (background included).</summary>
    public RectTransform Panel { get; }

    /// <summary>Builder for the page's content area, in the computer's own style.</summary>
    public UiBuilder Ui { get; }

    internal ComputerAppContext(ComputerApp app, RectTransform panel, UiBuilder ui)
    {
        App = app;
        Panel = panel;
        Ui = ui;
    }
}
