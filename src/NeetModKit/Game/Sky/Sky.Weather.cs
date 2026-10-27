using System;
using NeetLoader;
using NeetLoader.Runtime;
using NeetModKit.Events;
using NeetModKit.Features;

namespace NeetModKit.Game;

// The weather state: kept here, driven by one mod, read by the rest. It changes nothing in the scene.
public static partial class Sky
{
    private static WeatherState _weather = WeatherState.Default;
    private static ITracker _weatherOwner;

    /// <summary>The weather now. A steady <see cref="WeatherKind.Rain"/> unless a mod drives it.</summary>
    public static WeatherState Weather => _weather;

    /// <summary>Raised with the new state whenever <see cref="Weather"/> changes.</summary>
    public static readonly GameEvent<WeatherState> WeatherChanged = new GameEvent<WeatherState>(nameof(WeatherChanged));

    /// <summary>
    /// Id of the mod that last set the weather (a feature counts as its mod), or null for the default.
    /// </summary>
    public static string WeatherOwner => _weatherOwner switch
    {
        NeetMod mod => mod.Info.Id,
        ModFeature feature => feature.Owner?.Info.Id,
        _ => null,
    };

    /// <summary>
    /// Sets the weather. This is for the mod that drives it (the Weather mod on the host, Co-op on a client
    /// following the host); the last mod to set it owns it. The state goes back to the default when
    /// that mod unloads. Nothing in the scene changes: mods read <see cref="Weather"/> and apply their own effects.
    /// </summary>
    public static void SetWeather(ITracker owner, WeatherState state)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (_weatherOwner != owner)
        {
            _weatherOwner = owner;
            owner.Track(Disposable.Create(() => ReleaseWeather(owner)));
        }
        Change(state);
    }

    /// <summary>Starts <paramref name="kind"/> now, with no forecast.</summary>
    public static void SetWeather(ITracker owner, WeatherKind kind, float intensity = 1f) =>
        SetWeather(owner, WeatherState.Now(kind, intensity));

    private static void ReleaseWeather(ITracker owner)
    {
        if (_weatherOwner != owner)
            return;
        _weatherOwner = null;
        Change(WeatherState.Default);
    }

    private static void Change(WeatherState state)
    {
        if (_weather.Equals(state))
            return;
        _weather = state;
        WeatherChanged.Raise(state, KitMod.Instance?.Log);
    }
}
