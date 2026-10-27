using NeetModKit.Features;

namespace WeatherExpansion.Fog;

/// <summary>During a lightning flash the fog and the sky light up with it (the sky shows the landscape behind the yard).</summary>
internal sealed class LightningGlowFeature : ModFeature
{
    public const string FeatureId = "LightningGlow";

    public override string Id => FeatureId;
    public override string Name => "Lightning glow";
    public override string Description =>
        "Experimental. A lightning flash lights up the fog and the sky as well as the things near you, so the far " +
        "end of the yard doesn't stay black while the house goes white. Strength is in Settings.";

    protected override void OnEnable() => FogLook.Glow = true;

    protected override void OnDisable() => FogLook.Glow = false;
}
