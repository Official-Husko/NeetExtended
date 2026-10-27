using System.ComponentModel;
using NeetModKit.Settings;

namespace WeatherExpansion;

/// <summary>Stored in Mods/WeatherExpansion/config.json and shown in the mod menu. All of it is about how your own game looks.</summary>
public sealed class WeatherExpansionConfig
{
    [Label("Fog thickness")]
    [Range(0.5f, 3f)]
    [Description("How thick the fog is while \"Stormy fog\" is on. 1 is as thick as the game's own fog.")]
    public float FogThickness = 1.3f;

    [Label("Fog brightness")]
    [Range(0f, 2f)]
    [Description("How much of a glow the fog has at night while \"Stormy fog\" is on. 0 is the game's pitch black.")]
    public float FogBrightness = 1f;

    [Label("Lightning glow")]
    [Range(0f, 2f)]
    [Description("How strongly the fog and the sky light up with each lightning flash while \"Lightning glow\" is on.")]
    public float LightningGlow = 1f;

    [Label("Mist")]
    [Range(0f, 2f)]
    [Description("How much mist drifts over the front and back yard while \"Mist in the yards\" is on.")]
    public float Mist = 1f;
}
