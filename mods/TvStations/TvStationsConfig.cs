using System.ComponentModel;
using NeetModKit.Settings;

namespace TvStations;

/// <summary>Stored in Mods/TvStations/config.json and shown in the mod menu.</summary>
public sealed class TvStationsConfig
{
    [Label("Station volume")]
    [Description("How loud stations play, on top of the TV's own volume and each station's volume.")]
    [Range(0f, 1f)]
    public float Volume = 1f;
}
