using NeetModKit.Game;
using UnityEngine;

namespace WeatherExpansion.Storm;

/// <summary>What the scene gets for a kind of weather: how hard it rains, how loud the wind is, how long the wait between strikes is.</summary>
internal readonly struct StormLevel
{
    public readonly float Rain;
    public readonly float Volume;
    public readonly float ThunderMin;
    public readonly float ThunderMax;

    public StormLevel(float rain, float volume, float thunderMin, float thunderMax)
    {
        Rain = rain;
        Volume = volume;
        ThunderMin = thunderMin;
        ThunderMax = thunderMax;
    }

    public static StormLevel Lerp(StormLevel a, StormLevel b, float t) => new StormLevel(
        Mathf.Lerp(a.Rain, b.Rain, t), Mathf.Lerp(a.Volume, b.Volume, t),
        Mathf.Lerp(a.ThunderMin, b.ThunderMin, t), Mathf.Lerp(a.ThunderMax, b.ThunderMax, t));
}

/// <summary>
/// The table behind the weather. Every kind has a usual level; intensity (1 is usual) scales it.
/// The game's own rain is 1 for the particles and 1 (outdoors) or 0.28 (indoors) for the storm
/// loop, and its thunder waits 30 to 180 seconds, which is what plain rain gets.
/// </summary>
internal static class StormLevels
{
    // The range a phase picks its intensity from, and what a cheat or another mod may ask for.
    public const float MinIntensity = 0.7f;
    public const float MaxIntensity = 1.3f;
    public const float LowestAccepted = 0.3f;
    public const float HighestAccepted = 2f;

    public static float RainBase(WeatherKind kind) => kind == WeatherKind.Drizzle ? 0.35f : kind == WeatherKind.Storm ? 2.2f : 1f;

    /// <summary>Rain particles as one number across kinds, so the next phase can start where the last one was.</summary>
    public static float RainOf(WeatherKind kind, float intensity) => RainBase(kind) * intensity;

    public static StormLevel Of(WeatherKind kind, float intensity)
    {
        intensity = Mathf.Clamp(intensity, LowestAccepted, HighestAccepted);
        // Heavier weather strikes a little more often, lighter a little less.
        float wait = Mathf.Max(0.5f, intensity);
        switch (kind)
        {
            case WeatherKind.Drizzle:
                return new StormLevel(0.35f * intensity, 0.6f * (0.8f + 0.2f * intensity), 120f / wait, 360f / wait);
            case WeatherKind.Storm:
                return new StormLevel(2.2f * intensity, 1.7f * (0.8f + 0.2f * intensity), 10f / wait, 40f / wait);
            default:
                return new StormLevel(intensity, 1f * (0.8f + 0.2f * intensity), 30f / wait, 150f / wait);
        }
    }
}
