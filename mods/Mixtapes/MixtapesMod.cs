using Mixtapes.Audio;
using Mixtapes.Features;
using Mixtapes.House;
using Mixtapes.Menu;
using Mixtapes.Radio;
using Mixtapes.Tapes;
using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Features;
using NeetModKit.IO;
using NeetModKit.Menu;
using NeetModKit.Settings;

namespace Mixtapes;

/// <summary>
/// Song files from any mod's "mixtapes" folder become cassettes for the radio, sold in the store
/// or left in the house, and the radio plays them like a station when there's no tape in it.
/// </summary>
public sealed class MixtapesMod : NeetMod
{
    internal static MixtapesMod Instance { get; private set; }
    internal static ConfigFile<MixtapesConfig> Config { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        Config = LoadConfig<MixtapesConfig>();
        Config.Save();
        Config.Changed += _ => TapeLoader.Reload();
        ModSettings.Register(this, Config, "Mixtapes");

        SongLibrary.Init(this);
        ModContent.FileArrived += SongLibrary.FileArrived;
        SongLibrary.SongLoaded += RadioStation.Refresh;
        TapeLoader.Changed += RadioStation.Refresh;
        TapeLoader.Init(this);
        HouseTapes.Init(this);

        ModFeatures.Register(this, new RadioStationFeature());
        ModMenu.AddPage(this, new MixtapesPage());
    }

    // After every mod has loaded, so their mixtapes folders are there too.
    public override void OnAllLoaded() => TapeLoader.Reload();

    public override void OnUnload()
    {
        ModContent.FileArrived -= SongLibrary.FileArrived;
        SongLibrary.SongLoaded -= RadioStation.Refresh;
        TapeLoader.Changed -= RadioStation.Refresh;
        RadioStation.StopAll();
        SongLibrary.Clear();
        Instance = null;
    }
}
