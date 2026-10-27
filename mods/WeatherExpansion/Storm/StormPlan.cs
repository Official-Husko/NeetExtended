using NeetModKit.Game;
using UnityEngine;

namespace WeatherExpansion.Storm;

/// <summary>One stretch of weather: what it is and how strong.</summary>
internal readonly struct StormPhase
{
    public readonly WeatherKind Kind;
    public readonly float Intensity;

    public StormPhase(WeatherKind kind, float intensity)
    {
        Kind = kind;
        Intensity = intensity;
    }
}

/// <summary>
/// Decides what the weather does next. Kinds only move one step (drizzle to rain to storm and back),
/// and a step keeps the strength of the rain close to where it was, so there is never a drizzle one
/// minute and a downpour the next. A phase can also stay the same kind and get lighter or heavier.
/// The weather never stops: there is no dry spell in the table.
/// </summary>
internal static class StormPlan
{
    private const float StayWeight = 0.8f;
    private const float StepWeight = 1.2f;
    private const float MinShift = 0.12f;
    private const float MaxShift = 0.3f;

    /// <summary>What comes after <paramref name="now"/>, within <paramref name="strongest"/>. <paramref name="storminess"/> leans it up (towards 1) or down (towards 0).</summary>
    public static StormPhase Next(StormPhase now, WeatherKind strongest, float storminess)
    {
        storminess = Mathf.Clamp01(storminess);
        var kind = now.Kind;
        float up = kind < strongest ? StepWeight * 2f * storminess : 0f;
        float down = kind > WeatherKind.Drizzle ? StepWeight * 2f * (1f - storminess) : 0f;
        // Stronger than allowed (the limit was lowered): the only way is down.
        float stay = kind > strongest ? 0f : StayWeight;
        if (kind > strongest)
            down = Mathf.Max(down, 1f);
        float total = up + down + stay;
        if (total <= 0.001f)
            return new StormPhase(kind, Shift(now.Intensity));

        float pick = Random.value * total;
        if (pick < up)
            return Move(now, kind + 1);
        if (pick < up + down)
            return Move(now, kind - 1);
        return new StormPhase(kind, Shift(now.Intensity));
    }

    /// <summary>A phase of <paramref name="kind"/> that starts about as wet as <paramref name="now"/> is.</summary>
    private static StormPhase Move(StormPhase now, WeatherKind kind)
    {
        float wanted = StormLevels.RainOf(now.Kind, now.Intensity) / StormLevels.RainBase(kind);
        float intensity = Mathf.Clamp(wanted + Random.Range(-0.1f, 0.1f), StormLevels.MinIntensity, StormLevels.MaxIntensity);
        return new StormPhase(kind, intensity);
    }

    private static float Shift(float intensity)
    {
        float step = Random.Range(MinShift, MaxShift) * (Random.value < 0.5f ? -1f : 1f);
        float next = intensity + step;
        // Bounced off an end: go the other way instead of sitting on it.
        if (next < StormLevels.MinIntensity || next > StormLevels.MaxIntensity)
            next = intensity - step;
        return Mathf.Clamp(next, StormLevels.MinIntensity, StormLevels.MaxIntensity);
    }

    /// <summary>How long a phase lasts: half to one and a half times the setting.</summary>
    public static float Duration(float averageMinutes) => Mathf.Max(10f, averageMinutes * 60f * Random.Range(0.5f, 1.5f));

    /// <summary>How long the change into a phase takes: a third of it, at least 6 and at most 45 seconds.</summary>
    public static float RampFor(float phaseSeconds) => Mathf.Clamp(phaseSeconds * 0.3f, 6f, 45f);
}
