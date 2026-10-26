using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Features;
using NeetModKit.Menu;
using NeetModKit.Settings;
using TvStations.Features;
using TvStations.Menu;
using TvStations.Stations;
using TvStations.Tv;

namespace TvStations;

/// <summary>
/// Video files from any mod's "tv" folder become stations on the living room TV, next to or in
/// place of the game's channels.
/// </summary>
public sealed class TvStationsMod : NeetMod
{
    internal static TvStationsMod Instance { get; private set; }
    internal static ConfigFile<TvStationsConfig> Config { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        TvChannels.Start();
        Config = LoadConfig<TvStationsConfig>();
        Config.Save();
        Config.Changed += c =>
        {
            TvChannels.Volume = c.Volume;
            TvChannels.ApplyVolume();
        };
        TvChannels.Volume = Config.Value.Volume;
        ModSettings.Register(this, Config, "Custom TV Stations");

        StationLoader.Init(this);
        ModFeatures.Register(this, new GameChannelsFeature());
        ModMenu.AddPage(this, new TvPage());
    }

    // After every mod has loaded, so their tv folders are there too.
    public override void OnAllLoaded() => StationLoader.Reload();

    public override void OnUnload()
    {
        TvChannels.Restore();
        Instance = null;
    }
}
