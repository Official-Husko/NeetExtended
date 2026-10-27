using NeetModKit.Features;

namespace WeatherExpansion.Fog;

/// <summary>The night's fog gets a dark blue-grey colour instead of black, a thickness you choose, and a slow drift.</summary>
internal sealed class StormFogFeature : ModFeature
{
    public const string FeatureId = "StormFog";

    public override string Id => FeatureId;
    public override string Name => "Stormy fog";
    public override string Description =>
        "Experimental. The fog is a dark blue-grey instead of pitch black, so the house and the trees show as shapes " +
        "in the haze, and it slowly thickens and thins. Thickness and brightness are in Settings.";

    protected override void OnEnable() => FogLook.Haze = true;

    protected override void OnDisable() => FogLook.Haze = false;
}
