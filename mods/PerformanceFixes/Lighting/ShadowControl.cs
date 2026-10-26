using System.Collections.Generic;
using PerformanceFixes.Common;
using UnityEngine;

namespace PerformanceFixes.Lighting;

/// <summary>
/// Takes shadows away from lights according to <see cref="ShadowPreset"/>, remembering what each
/// light had so it can be put back. A shadowed point light renders everything in its range six
/// times (once per cube face). Besides the ceiling lights, the house has shadowed point lights in
/// both fridges (range 12), the microwave and every corpse (HouseManager.SoulifyCorpse).
/// </summary>
internal static class ShadowControl
{
    private static readonly Dictionary<Light, LightShadows> Changed = new Dictionary<Light, LightShadows>(ByReference<Light>.Instance);
    private static readonly HashSet<Light> RoomLights = new HashSet<Light>(ByReference<Light>.Instance);
    private static ShadowPreset _preset = ShadowPreset.AllLights;
    private static ShadowQuality? _qualityBefore;

    public static int Count => Changed.Count;

    /// <summary>Applies a preset to every light in the scene. Run when the house is ready and when the setting changes.</summary>
    public static void Apply(ShadowPreset preset)
    {
        Restore();
        _preset = preset;
        if (preset == ShadowPreset.None)
        {
            _qualityBefore = QualitySettings.shadows;
            QualitySettings.shadows = ShadowQuality.Disable;
            return;
        }
        if (preset != ShadowPreset.RoomLights)
            return;

        RoomLights.Clear();
        // Lights wired to a switch (ceiling lights, lamps) keep their shadows.
        foreach (var lightSwitch in Object.FindObjectsOfType<LightSwitch>())
        {
            if (lightSwitch.linkedLights != null)
                RoomLights.UnionWith(lightSwitch.linkedLights);
        }
        foreach (var light in Object.FindObjectsOfType<Light>())
            Consider(light);
    }

    /// <summary>For lights that appear later: spawned items and corpses.</summary>
    public static void Spawned(GameObject root)
    {
        if (_preset != ShadowPreset.RoomLights || root == null)
            return;
        foreach (var light in root.GetComponentsInChildren<Light>(true))
            Consider(light);
    }

    /// <summary>Gives every light its shadows back.</summary>
    public static void Restore()
    {
        foreach (var pair in Changed)
        {
            if (pair.Key != null)
                pair.Key.shadows = pair.Value;
        }
        Changed.Clear();
        RoomLights.Clear();
        if (_qualityBefore.HasValue)
            QualitySettings.shadows = _qualityBefore.Value;
        _qualityBefore = null;
        _preset = ShadowPreset.AllLights;
    }

    /// <summary>The scene's lights are gone; forget them without touching anything.</summary>
    public static void Forget()
    {
        Changed.Clear();
        RoomLights.Clear();
    }

    // Spot lights (the flashlight) and directional lights (the moon) render one shadow map, not six.
    private static void Consider(Light light)
    {
        if (light.shadows == LightShadows.None || light.type != LightType.Point || RoomLights.Contains(light) || Changed.ContainsKey(light))
            return;
        Changed[light] = light.shadows;
        light.shadows = LightShadows.None;
    }
}
