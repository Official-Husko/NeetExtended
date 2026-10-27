using NeetModKit.Events;
using NeetModKit.Features;
using PerformanceFixes.Lighting;
using UnityEngine.SceneManagement;

namespace PerformanceFixes.Features;

/// <summary>See <see cref="MoonShadows"/>. Which shadows the moon keeps is the Moon shadows setting.</summary>
internal sealed class MoonShadowsFeature : ModFeature
{
    public const string FeatureId = "MoonShadows";

    public override string Id => FeatureId;
    public override string Name => "Fewer moon shadows";
    public override string Description =>
        "The moon is the house's only outdoor light and it casts soft shadows over the whole scene, which costs " +
        "a good part of each frame for a light that is almost invisible. With this, the Moon shadows setting " +
        "decides when it casts them.";

    protected override void OnEnable()
    {
        GameEvents.WhenHouseReady(this, Apply);
        PerformanceFixesMod.Config.Changed += OnSettingsChanged;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    protected override void OnDisable()
    {
        PerformanceFixesMod.Config.Changed -= OnSettingsChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        MoonShadows.Restore();
    }

    public override void OnUpdate() => MoonShadows.Tick();

    private static void Apply() => MoonShadows.Apply(PerformanceFixesMod.Config.Value.MoonShadows);

    private static void OnSettingsChanged(PerformanceConfig config)
    {
        if (GameEvents.IsHouseReady)
            MoonShadows.Apply(config.MoonShadows);
    }

    private static void OnSceneUnloaded(Scene scene) => MoonShadows.Forget();
}
