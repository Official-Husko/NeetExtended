using NeetModKit.Features;
using NeetModKit.Game;
using UnityEngine;
using WeatherExpansion.Storm;

namespace WeatherExpansion.Fog;

/// <summary>
/// The night's fog gets a dark blue-grey colour instead of black (<see cref="FogLook"/>) and a thickness
/// that follows the thickness setting, the weather and a slow drift. The thickness goes to the framework as
/// this feature's own <c>Sky.FogAmount</c> factor, so it combines with any other mod's and is put back when
/// the feature goes off.
/// </summary>
internal sealed class StormFogFeature : ModFeature
{
    public const string FeatureId = "StormFog";
    private float _applied = -1f;

    public override string Id => FeatureId;
    public override string Name => "Stormy fog";
    public override string Description =>
        "Experimental. The fog is a dark blue-grey instead of pitch black, so the house and the trees show as shapes " +
        "in the haze. It slowly thickens and thins, and it follows the weather: thinner in a drizzle, thicker in a storm. " +
        "Thickness and brightness are in Settings.";

    protected override void OnEnable()
    {
        FogLook.Haze = true;
        _applied = -1f;
    }

    protected override void OnDisable()
    {
        FogLook.Haze = false;
        Sky.FogAmount.Clear(this);
    }

    public override void OnUpdate()
    {
        float factor = WeatherExpansionMod.Config.Value.FogThickness * WeatherFollow.Fog * Drift(Time.time);
        if (Mathf.Abs(factor - _applied) < 0.002f)
            return;
        _applied = factor;
        Sky.FogAmount.Set(this, factor);
    }

    // The fog thins and thickens slowly, a different rhythm every minute or so.
    private static float Drift(float time) => 1f + 0.16f * Mathf.Sin(time * 0.13f) * Mathf.Sin(time * 0.071f + 1.3f);
}
