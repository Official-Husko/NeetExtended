using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Features;
using NeetModKit.Settings;
using UnityEngine.SceneManagement;
using WeatherExpansion.Fog;
using WeatherExpansion.Windows;

namespace WeatherExpansion;

/// <summary>
/// How the storm looks. The house is one rainy night; this mod adds looks to it, each one a feature
/// that can be switched off on the Mods page. Everything here is drawn by the local game, so in
/// co-op every player sees their own copy.
/// </summary>
public sealed class WeatherExpansionMod : NeetMod
{
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
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    public override void OnUpdate() => FogLook.Tick(Config.Value);

    public override void OnUnload()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Instance = null;
    }

    private static void OnSceneUnloaded(Scene scene) => FogLook.SceneGone();
}
