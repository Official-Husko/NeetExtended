using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Features;
using NeetModKit.Settings;
using PerformanceFixes.Features;
using PerformanceFixes.Painting;

namespace PerformanceFixes;

/// <summary>
/// Fixes for what makes the game hitch or run slow, measured in docs/research/performance.md.
/// Each fix is a feature that can be switched off on the Mods page.
/// </summary>
public sealed class PerformanceFixesMod : NeetMod
{
    internal static PerformanceFixesMod Instance { get; private set; }
    internal static ConfigFile<PerformanceConfig> Config { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        Config = LoadConfig<PerformanceConfig>();
        Config.Save();
        ModSettings.Register(this, Config, "Performance Fixes");

        ModFeatures.Register(this, new FastPaintSavesFeature());
        ModFeatures.Register(this, new TextureLeaksFeature());
        ModFeatures.Register(this, new PeepholeFeature());
        ModFeatures.Register(this, new ShadowsFeature());
        ModFeatures.Register(this, new LogSpamFeature());
    }

    // Paint pictures still being written must reach the disk before the process ends.
    public override void OnQuit() => PaintSaver.Flush();

    public override void OnUnload() => Instance = null;
}
