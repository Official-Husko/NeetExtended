using NeetModKit.Events;
using NeetModKit.Features;
using PerformanceFixes.Lighting;
using UnityEngine.SceneManagement;

namespace PerformanceFixes.Features;

/// <summary>See <see cref="ShadowControl"/>. Which lights keep shadows is the Shadows setting.</summary>
internal sealed class ShadowsFeature : ModFeature
{
    public const string FeatureId = "Shadows";

    public override string Id => FeatureId;
    public override string Name => "Fewer shadows";
    public override string Description =>
        "Shadows from the house's lights are the biggest part of each frame, and not only the ceiling lights cast " +
        "them: both fridges, the microwave and every corpse do too. With this, only the lights picked in the Shadows " +
        "setting cast them.";

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
        ShadowControl.Restore();
    }

    private static void Apply() => ShadowControl.Apply(PerformanceFixesMod.Config.Value.Shadows);

    private static void OnSettingsChanged(PerformanceConfig config)
    {
        if (GameEvents.IsHouseReady)
            ShadowControl.Apply(config.Shadows);
    }

    private static void OnSceneUnloaded(Scene scene) => ShadowControl.Forget();
}
