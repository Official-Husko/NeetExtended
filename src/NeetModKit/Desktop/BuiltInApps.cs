using System.Collections.Generic;

namespace NeetModKit.Desktop;

/// <summary>Names and descriptions for the game's own websites, keyed by their page object's name.</summary>
internal static class BuiltInApps
{
    public static readonly Dictionary<string, (string Title, string Description)> Known = new Dictionary<string, (string, string)>
    {
        ["Home"] = ("Metscape", "The browser's home page."),
        ["PunStore"] = ("Pun Store", "The normal online store."),
        ["BlackMarket"] = ("Costello's", "The black market. Orders can attract the police."),
        ["OccultMarket"] = ("Katacum", "The occult market. Orders can attract the police."),
        ["AnimeClicker"] = ("Anime Clicker", "The anime card idle game."),
        ["Downzone"] = ("Downzone", "Buys and installs computer apps."),
        ["BuddyPortal"] = ("Buddy Portal", "The Pig Buddy site."),
        ["KustomSHT"] = ("Kustom SHT", "Items printed with your own pictures."),
        ["STRONK"] = ("STRONK", "The stock market."),
        ["CUM"] = ("CUM", "A site from the original game."),
        ["LuckyCassette"] = ("Lucky Cassette", "Cassettes with your own music."),
    };

    public static (string Title, string Description) Describe(string panelName) =>
        Known.TryGetValue(panelName, out var info) ? info : (panelName, "A site from the original game.");
}
