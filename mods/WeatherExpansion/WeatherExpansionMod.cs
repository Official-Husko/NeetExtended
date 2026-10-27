using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Features;
using NeetModKit.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeatherExpansion.Storm;
using WeatherExpansion.Fog;
using WeatherExpansion.Lightning;
using WeatherExpansion.Forecast;
using NeetModKit.Saves;
using WeatherExpansion.Windows;

namespace WeatherExpansion;

/// <summary>
/// How the storm looks. The house is one rainy night; this mod adds looks to it, each one a feature
/// that can be switched off on the Mods page. Everything here is drawn by the local game, so in
/// co-op every player sees their own copy.
/// </summary>
public sealed class WeatherExpansionMod : NeetMod
{
    private const string SaveKey = "WeatherExpansion.Storm";

    internal static WeatherExpansionMod Instance { get; private set; }
    internal static ConfigFile<WeatherExpansionConfig> Config { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        Config = LoadConfig<WeatherExpansionConfig>();
        Config.Save();
        ModSettings.Register(this, Config, "Weather Expansion");

        ModFeatures.Register(this, new StormFogFeature());
        ModFeatures.Register(this, new LightningGlowFeature());
        ModFeatures.Register(this, new MistBanksFeature());
        ModFeatures.Register(this, new RainOnGlassFeature());
        ModFeatures.Register(this, new TvStaticFeature());
        ModFeatures.Register(this, new RadioCrackleFeature());
        ModFeatures.Register(this, new PhoneStaticFeature());
        ModFeatures.Register(this, new FusePopFeature());
        ForecastApp.Register(this);
        ModFeatures.Register(this, new StormScheduleFeature());
        ModSaveData.Register<StormMemory.Saved>(this, StormMemory.ForSave, StormMemory.FromSave, SaveKey);
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    public override void OnUpdate()
    {
        WeatherFollow.Tick(Time.deltaTime);
        FogLook.Tick(Config.Value);
        ForecastApp.Tick();
    }

    public override void OnUnload()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        CrackleClip.Release();
        ForecastApp.Release();
        Instance = null;
    }

    private static void OnSceneUnloaded(Scene scene) => FogLook.SceneGone();
}
