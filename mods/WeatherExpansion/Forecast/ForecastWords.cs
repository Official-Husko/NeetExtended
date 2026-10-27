using NeetModKit.Game;
using UnityEngine;

namespace WeatherExpansion.Forecast;

/// <summary>The forecast site's copy: a line about each kind of weather, the "then" line and the clock text. Nothing here touches the game.</summary>
internal static class ForecastWords
{
    private static readonly string[][] Moods =
    {
        // Drizzle
        new[] { "Barely wet. Rain for pussies.", "A baby is peeing on the roof. A small one.", "Wet enough to ruin a hairdo, not a day." },
        // Rain
        new[] { "Proper wet. Socks are not safe.", "God left the hose on and went to bed.", "Rain. The plain kind. Boring and soggy." },
        // Storm
        new[] { "HOLY SHIT. Stay inside, dickhead.", "The sky is throwing up on your house.", "Thunder, wind, the whole dick. Hide the pets." },
    };

    private static readonly Color[] Colors =
    {
        new Color(0.55f, 0.8f, 1f),
        new Color(0.3f, 0.55f, 1f),
        new Color(1f, 0.45f, 0.2f),
    };

    private static readonly string[] Then =
    {
        "Nobody knows what's next. Probably more wet.",
        "Machine's broken. Someone put a sandwich in it.",
        "No idea. My ass itches, so maybe rain.",
    };

    public static string Mood(WeatherKind kind, int pick)
    {
        var lines = Moods[(int)kind];
        return lines[pick % lines.Length];
    }

    public static int PickCount(WeatherKind kind) => Moods[(int)kind].Length;

    public static Color ColorOf(WeatherKind kind) => Colors[(int)kind];

    public static string NoForecast(int pick) => Then[pick % Then.Length];

    /// <summary>"RAIN (light)" or "STORM (heavy)" when the strength is away from the usual.</summary>
    public static string Now(WeatherState weather)
    {
        string name = weather.Name.ToUpperInvariant();
        if (weather.Intensity >= 1.25f)
            return name + " (heavy)";
        if (weather.Intensity <= 0.75f)
            return name + " (light)";
        return name;
    }

    /// <summary>Whole seconds as m:ss.</summary>
    public static string Clock(int seconds)
    {
        if (seconds < 0)
            seconds = 0;
        return (seconds / 60) + ":" + (seconds % 60).ToString("00");
    }
}
