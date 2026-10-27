using System;
using NeetModKit.Rules;

namespace NeetModKit.Game;

/// <summary>How hard it rains. The house is always in the rain; the kinds say how much.</summary>
public enum WeatherKind
{
    Drizzle,
    Rain,
    Storm,
}

/// <summary>
/// What the weather is now and, when a mod plans ahead, what comes next. A plain value: the framework
/// only keeps it (<see cref="Sky.Weather"/>) for the mod that drives the weather and everyone who
/// reads it. Times are in play seconds (<see cref="ModFlags.PlaySeconds"/>).
/// </summary>
public readonly struct WeatherState : IEquatable<WeatherState>
{
    /// <summary>What the house has with no mod: a steady rain.</summary>
    public static readonly WeatherState Default = new WeatherState(WeatherKind.Rain, 1f, 0f, null, 0f);

    public WeatherState(WeatherKind kind, float intensity, float startedAt, WeatherKind? nextKind, float nextAt)
    {
        Kind = kind;
        Intensity = float.IsNaN(intensity) ? 1f : Math.Max(0f, intensity);
        StartedAt = startedAt;
        NextKind = nextKind;
        NextAt = nextAt;
    }

    public WeatherKind Kind { get; }

    /// <summary>1 is the usual strength for the kind, below 1 lighter, above 1 heavier.</summary>
    public float Intensity { get; }

    /// <summary>When this weather began.</summary>
    public float StartedAt { get; }

    /// <summary>What the forecast says follows, or null when nobody forecasts.</summary>
    public WeatherKind? NextKind { get; }

    /// <summary>When the forecast change happens (only meaningful with <see cref="NextKind"/>).</summary>
    public float NextAt { get; }

    public bool HasForecast => NextKind.HasValue;

    /// <summary>A state that starts right now.</summary>
    public static WeatherState Now(WeatherKind kind, float intensity = 1f, WeatherKind? next = null, float nextAt = 0f) =>
        new WeatherState(kind, intensity, ModFlags.PlaySeconds, next, nextAt);

    /// <summary>Seconds since this weather began.</summary>
    public float Age => ModFlags.PlaySeconds - StartedAt;

    /// <summary>Seconds until the forecast change; -1 without a forecast.</summary>
    public float SecondsUntilNext => HasForecast ? Math.Max(0f, NextAt - ModFlags.PlaySeconds) : -1f;

    /// <summary>The word players read: "drizzle", "rain" or "storm".</summary>
    public string Name => NameOf(Kind);

    /// <summary>The forecast's word, or <see cref="Name"/> when there is none.</summary>
    public string NextName => NextKind.HasValue ? NameOf(NextKind.Value) : Name;

    public static string NameOf(WeatherKind kind)
    {
        switch (kind)
        {
            case WeatherKind.Drizzle: return "drizzle";
            case WeatherKind.Storm: return "storm";
            default: return "rain";
        }
    }

    public static bool TryParse(string text, out WeatherKind kind)
    {
        return Enum.TryParse(text?.Trim(), true, out kind) && Enum.IsDefined(typeof(WeatherKind), kind);
    }

    public bool Equals(WeatherState other) =>
        Kind == other.Kind && Intensity == other.Intensity && StartedAt == other.StartedAt &&
        NextKind == other.NextKind && (!NextKind.HasValue || NextAt == other.NextAt);

    public override bool Equals(object obj) => obj is WeatherState other && Equals(other);

    public override int GetHashCode() => ((int)Kind * 397) ^ Intensity.GetHashCode();

    public override string ToString() => $"{Name} x{Intensity:0.##}" + (HasForecast ? $", then {NameOf(NextKind.Value)}" : "");
}
