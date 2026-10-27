using System.ComponentModel;
using NeetModKit.Game;
using NeetModKit.Settings;

namespace WeatherExpansion;

/// <summary>Stored in Mods/WeatherExpansion/config.json and shown in the mod menu. The looks and the TV, radio and phone effects are about your own game; the fuse pop changes the house, and the settings marked shared shape the storm itself (in co-op the host's count).</summary>
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

    [Label("Rain on the windows")]
    [Range(0f, 1f)]
    [Description("How many drops sit on the windows and how often one starts to run, while \"Rain on the windows\" is on.")]
    public float WindowRain = 0.5f;

    [Label("Window haze")]
    [Range(0f, 0.4f)]
    [Description("How fogged up the glass is, so the drops show at night too. 0 is clear glass.")]
    public float WindowHaze = 0.15f;

    [Label("TV static time")]
    [Range(0f, 3f)]
    [Description("How many seconds the TV shows static after a lightning strike while \"Lightning static on the TV\" is on. 0 is none.")]
    public float TvStaticSeconds = 1f;

    [Label("Radio crackle")]
    [Range(0f, 1f)]
    [Description("How loud the static is and how far the music drops out after a strike while \"Lightning crackle on the radio\" is on. 0 is none.")]
    public float RadioCrackle = 0.7f;

    [Label("Static in phone calls")]
    [Description("A call in progress gets a burst of static from the phone after a strike (the screens always garble). The call is never cut.")]
    public bool PhoneCallStatic = true;

    [Label("Fuse pop chance")]
    [Range(0f, 1f)]
    [Description("The chance that a strike trips a breaker in the fuse box while \"Lightning pops a fuse\" is on (it is off by default). 0.1 is one strike in ten.")]
    public float FusePopChance = 0.1f;
    [Label("Weather changes every (minutes)")]
    [Range(1f, 20f)]
    [Description("About how long a spell of weather lasts before it changes, while \"Changing weather\" is on. Each spell lasts between half and one and a half times this.")]
    [Shared]
    public float WeatherMinutes = 4f;

    [Label("Strongest weather")]
    [Description("The heaviest weather the night gets to: drizzle, rain or storm.")]
    [Shared]
    public WeatherKind StrongestWeather = WeatherKind.Storm;

    [Label("Storminess")]
    [Range(0f, 1f)]
    [Description("Which way the weather leans. 0.5 is even, towards 1 it spends more time in heavy rain and storms, towards 0 more in drizzle.")]
    [Shared]
    public float Storminess = 0.5f;

    [Label("Thunder frequency")]
    [Range(0.25f, 4f)]
    [Description("How often lightning strikes, compared to what the weather usually brings. 2 is twice as often, 0.5 half as often.")]
    [Shared]
    public float ThunderFrequency = 1f;
}
