using System.ComponentModel;
using NeetModKit.Settings;

namespace PerformanceFixes;

public enum ShadowPreset
{
    /// <summary>As the game ships.</summary>
    AllLights,

    /// <summary>Lights on a switch and spot lights keep their shadows; fridges, the microwave and corpses don't.</summary>
    RoomLights,

    /// <summary>No shadows at all.</summary>
    None,
}

public enum MoonShadowMode
{
    /// <summary>As the game ships.</summary>
    On,

    /// <summary>The moon casts no shadows (a lightning flash still does) and is off indoors.</summary>
    Off,

    /// <summary>The moon casts shadows while the player is in the front or back yard, and is off indoors.</summary>
    OnlyOutdoors,
}

/// <summary>Stored in Mods/PerformanceFixes/config.json and shown in the mod menu.</summary>
public sealed class PerformanceConfig
{
    [Label("Shadows")]
    [Description("Which lights cast shadows while \"Fewer shadows\" is on. Room lights: lights you switch on and off and " +
                 "the flashlight keep theirs; the fridge lights, the microwave and corpses don't. None is the fastest. The moon has " +
                 "its own setting, Moon shadows.")]
    public ShadowPreset Shadows = ShadowPreset.RoomLights;

    [Label("Moon shadows")]
    [Description("When the moon casts shadows while \"Fewer moon shadows\" is on. On is the game's own look. Only outdoors: shadows " +
                 "in the yards, and the moon is off inside, where it reaches nothing. Off: no moon shadows anywhere. Lightning " +
                 "flashes keep theirs.")]
    public MoonShadowMode MoonShadows = MoonShadowMode.OnlyOutdoors;
}
