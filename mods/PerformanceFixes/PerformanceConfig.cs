using System.ComponentModel;
using NeetModKit.Settings;

namespace PerformanceFixes;

public enum ShadowPreset
{
    /// <summary>As the game ships.</summary>
    AllLights,

    /// <summary>Lights on a switch, spot lights and the moon keep their shadows; fridges, the microwave and corpses don't.</summary>
    RoomLights,

    /// <summary>No shadows at all.</summary>
    None,
}

/// <summary>Stored in Mods/PerformanceFixes/config.json and shown in the mod menu.</summary>
public sealed class PerformanceConfig
{
    [Label("Shadows")]
    [Description("Which lights cast shadows while \"Fewer shadows\" is on. Room lights: lights you switch on and off, the " +
                 "flashlight and the moon keep theirs; the fridge lights, the microwave and corpses don't. None is the fastest.")]
    public ShadowPreset Shadows = ShadowPreset.RoomLights;
}
