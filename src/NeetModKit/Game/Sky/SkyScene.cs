using System;
using System.Collections.Generic;
using NeetLoader.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeetModKit.Game;

/// <summary>
/// The sky of the scene that's loaded: the two lights, the rain systems, the fog and the storm loop,
/// with the values the scene started with, so the multipliers always scale the original and
/// putting them back restores it. Found once per house load, not every frame.
/// </summary>
internal static class SkyScene
{
    private const string ReloadKey = "NeetModKit.SkyScene";
    private const float DefaultMoon = 0.2f;
    private const float IndoorStormVolume = 0.28f;

    private static bool _captured;
    private static Light _moon;
    private static Light _sun;
    private static float _moonBase = -1f;
    private static float _fogBase;
    private static readonly List<ParticleSystem> Rain = new List<ParticleSystem>();
    private static readonly List<ParticleSystem.MinMaxCurve> RainRate = new List<ParticleSystem.MinMaxCurve>();
    private static readonly List<int> RainMax = new List<int>();

    public static Light Moon
    {
        get
        {
            Ensure();
            return _moon;
        }
    }

    public static Light Sun
    {
        get
        {
            Ensure();
            return _sun;
        }
    }

    /// <summary>The Moon's intensity as the scene has it (what a strike must leave behind).</summary>
    public static float MoonBase
    {
        get
        {
            Ensure();
            if (_moonBase > 0f)
                return _moonBase;
            // Never seen the scene's own value (a strike was already running): the game's number.
            return _moon != null && _moon.intensity > 0f && _moon.intensity <= 1f ? _moon.intensity : DefaultMoon;
        }
    }

    public static float FogBase
    {
        get
        {
            Ensure();
            return _fogBase;
        }
    }

    public static IReadOnlyList<ParticleSystem> RainSystems
    {
        get
        {
            Ensure();
            return Rain;
        }
    }

    /// <summary>Finds the scene's sky and remembers its own values. Does nothing when already done for this scene.</summary>
    public static void Ensure()
    {
        if (_captured)
            return;
        var house = HouseManager._;
        if (house == null)
            return;
        Capture(house);
    }

    /// <summary>The scene changed; the next <see cref="Ensure"/> looks again.</summary>
    public static void Forget()
    {
        _captured = false;
        _moon = null;
        _sun = null;
        _moonBase = -1f;
        Rain.Clear();
        RainRate.Clear();
        RainMax.Clear();
    }

    private static void Capture(HouseManager house)
    {
        _captured = true;
        _moon = house.mainDirectionalLight;
        _moonBase = _moon != null ? _moon.intensity : -1f;
        _fogBase = RenderSettings.fogDensity;
        FindRain();
        FindSun();
        // A copy of the kit that was replaced while this scene ran kept the scene's own values.
        if (ReloadState.Take(ReloadKey, out float[] kept))
            ApplyKept(kept);
        else if (_moonBase > 1f)
            _moonBase = DefaultMoon;
        ApplyAll();
    }

    /// <summary>Writes the multipliers set so far into a scene that has just been found.</summary>
    private static void ApplyAll()
    {
        Sky.RainAmount.Applied = !Mathf.Approximately(Sky.RainAmount.Value, 1f);
        Sky.FogAmount.Applied = !Mathf.Approximately(Sky.FogAmount.Value, 1f);
        Sky.StormVolume.Applied = !Mathf.Approximately(Sky.StormVolume.Value, 1f);
        ApplyRain();
        ApplyFog();
        ApplyStormVolume();
    }

    private static void FindRain()
    {
        Rain.Clear();
        RainRate.Clear();
        RainMax.Clear();
        var found = new List<ParticleSystem>();
        foreach (var system in Resources.FindObjectsOfTypeAll<ParticleSystem>())
        {
            if (system.gameObject.scene.IsValid() && system.name.StartsWith("Particle_Rain", StringComparison.Ordinal))
                found.Add(system);
        }
        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (var system in found)
        {
            Rain.Add(system);
            RainRate.Add(system.emission.rateOverTime);
            RainMax.Add(system.main.maxParticles);
        }
    }

    private static void FindSun()
    {
        _sun = null;
        foreach (var light in Resources.FindObjectsOfTypeAll<Light>())
        {
            if (light.type == LightType.Directional && light != _moon && light.gameObject.scene.IsValid() &&
                light.name.IndexOf("Sun", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _sun = light;
                return;
            }
        }
    }

    public static void ApplyRain()
    {
        Ensure();
        var multiplier = Sky.RainAmount;
        if (!multiplier.Applied)
            return;
        float m = multiplier.Value;
        for (int i = 0; i < Rain.Count; i++)
        {
            var system = Rain[i];
            if (system == null)
                continue;
            var emission = system.emission;
            emission.rateOverTime = Scale(RainRate[i], m);
            var main = system.main;
            main.maxParticles = Mathf.Max(1, Mathf.CeilToInt(RainMax[i] * Mathf.Max(1f, m)));
        }
        if (Mathf.Approximately(m, 1f))
            multiplier.Applied = false;
    }

    public static void ApplyFog()
    {
        Ensure();
        var multiplier = Sky.FogAmount;
        if (!multiplier.Applied || !_captured)
            return;
        RenderSettings.fogDensity = _fogBase * multiplier.Value;
        if (Mathf.Approximately(multiplier.Value, 1f))
            multiplier.Applied = false;
    }

    /// <summary>
    /// Sets the storm loop's volume to the game's own for the room the player is in (BrainPlayer.cs:62-82
    /// sets 1 outside and 0.28 inside on every room change) times the multiplier.
    /// </summary>
    public static void ApplyStormVolume()
    {
        var multiplier = Sky.StormVolume;
        var house = HouseManager._;
        if (!multiplier.Applied || house == null || house.thunderStormAudio == null || house.noThunder)
            return;
        var brain = GameRefs.PlayerBrain as BrainPlayer;
        float own = brain != null && brain.isOutside ? 1f : IndoorStormVolume;
        house.thunderStormAudio.volume = Mathf.Clamp01(own * multiplier.Value);
        if (Mathf.Approximately(multiplier.Value, 1f))
            multiplier.Applied = false;
    }

    /// <summary>Puts everything back the way the scene had it and remembers that for a replacement copy of the kit.</summary>
    public static void Release()
    {
        if (_captured)
        {
            Sky.RainAmount.ClearAll();
            Sky.FogAmount.ClearAll();
            Sky.StormVolume.ClearAll();
            ReloadState.Put(ReloadKey, Keep());
        }
        Forget();
    }

    private static ParticleSystem.MinMaxCurve Scale(ParticleSystem.MinMaxCurve curve, float factor)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                curve.constant *= factor;
                break;
            case ParticleSystemCurveMode.TwoConstants:
                curve.constantMin *= factor;
                curve.constantMax *= factor;
                break;
            default:
                curve.curveMultiplier *= factor;
                break;
        }
        return curve;
    }

    // Kept for a reload: moon, fog, then the rain count (the rates are re-read from the restored scene).
    private static float[] Keep() => new[] { _moonBase, _fogBase, (float)SceneManager.GetActiveScene().buildIndex };

    private static void ApplyKept(float[] kept)
    {
        if (kept.Length < 3 || (int)kept[2] != SceneManager.GetActiveScene().buildIndex)
            return;
        _moonBase = kept[0];
        _fogBase = kept[1];
    }
}
