using System;
using NeetLoader;
using NeetLoader.Runtime;
using NeetModKit.Events;
using UnityEngine;

namespace NeetModKit.Game;

/// <summary>
/// The house's sky: the moon and sun lights, lightning, the thunder timer, rain, fog and the storm
/// loop, and a small weather state that one mod drives and others read. The house has one fixed
/// rainy night; this is the handle mods use to change how it looks and sounds without fighting
/// the game or each other. Everything is null-safe outside the house.
///
/// The rain, fog and storm volume are <see cref="SkyMultiplier"/>s: every mod sets its own factor and
/// the scene's own values come back when they're cleared or the mod unloads. The thunder timer is
/// <see cref="SetThunderDelay"/>. The weather is <see cref="Weather"/>, set with <see cref="SetWeather"/>
/// by the mod that owns it (it changes nothing in the scene by itself).
///
/// In co-op only the host's thunder loop and strikes are real: clients play the host's strikes and
/// <see cref="Strike"/> on a client returns false and does nothing.
/// </summary>
public static partial class Sky
{
    /// <summary>Raised when lightning strikes, after the flash peaks (the lamps flicker at the same moment). Survives house loads.</summary>
    public static readonly GameEvent OnStrike = new GameEvent(nameof(OnStrike));

    /// <summary>Scale on the rain's emission (all four systems). 0 is a dry house, 3 a downpour; the scene's own is 1.</summary>
    public static readonly SkyMultiplier RainAmount = new SkyMultiplier(SkyScene.ApplyRain);

    /// <summary>Scale on the scene's fog density (<c>RenderSettings.fogDensity</c>, 0.06 in the house).</summary>
    public static readonly SkyMultiplier FogAmount = new SkyMultiplier(SkyScene.ApplyFog);

    /// <summary>
    /// Scale on the storm loop's volume (the rain and wind the game plays at 1.0 outdoors and 0.28 inside).
    /// Applied after the game sets it on every room change; Unity caps the result at 1.
    /// </summary>
    public static readonly SkyMultiplier StormVolume = new SkyMultiplier(SkyScene.ApplyStormVolume);

    private static HouseManager _attachedTo;

    /// <summary>The moon: the one directional light that's on in the house (blue, 0.2). Null outside a scene that has one.</summary>
    public static Light Moon => SkyScene.Moon;

    /// <summary>The sun: an orange directional light the house has but keeps switched off. Null when the scene has none.</summary>
    public static Light Sun => SkyScene.Sun;

    /// <summary>The moon's intensity as the scene has it (0.2). A strike flashes it and leaves it here.</summary>
    public static float MoonBaseIntensity => SkyScene.MoonBase;

    /// <summary>The four rain particle systems (Particle_Rain_*), in name order; empty outside the house. Read-only: use <see cref="RainAmount"/> to change them.</summary>
    public static System.Collections.Generic.IReadOnlyList<ParticleSystem> RainSystems => SkyScene.RainSystems;

    /// <summary>The scene's fog density before any <see cref="FogAmount"/>.</summary>
    public static float SceneFogDensity => SkyScene.FogBase;

    /// <summary>
    /// Lightning now, the game's way: the same flash, lamp flicker and clap as a random one (the instant
    /// version of <c>HouseManager.Thunder</c>). The random loop isn't touched, so thunder carries on. It doesn't
    /// use the story's thunder sequence, which stops the loop for good without the Unofficial NEET Patches' ThunderLoop fix.
    /// False when there's no house to strike, or this is a co-op client (only the host's strikes are real there).
    /// </summary>
    public static bool Strike()
    {
        var house = HouseManager._;
        if (house == null || house.mainDirectionalLight == null || !WorldRole.RunsWorld)
            return false;
        SkyScene.Ensure();
        house.StartCoroutine(house.Thunder(instant: true));
        return true;
    }

    internal static void Init(NeetMod kit)
    {
        GameEvents.SceneLoaded.Subscribe(kit, _ =>
        {
            SkyScene.Forget();
            ThunderTiming.Reset();
        });
        GameEvents.WhenHouseReady(kit, AttachToHouse);
        SkyScene.Ensure(); // Reloaded while the house runs: take the scene's own values from the copy before.
        kit.Track(Disposable.Create(Shutdown));
    }

    private static void Shutdown()
    {
        if (_attachedTo != null)
            _attachedTo.OnLightningStrike -= RelayStrike;
        _attachedTo = null;
        SkyScene.Release();
        ThunderTiming.Shutdown();
    }

    // HouseManager is a new object every house load; follow it.
    private static void AttachToHouse()
    {
        var house = HouseManager._;
        if (house == null || house == _attachedTo)
            return;
        if (_attachedTo != null)
            _attachedTo.OnLightningStrike -= RelayStrike;
        house.OnLightningStrike += RelayStrike;
        _attachedTo = house;
    }

    private static void RelayStrike() => GameEvents.Raise(OnStrike);
}
