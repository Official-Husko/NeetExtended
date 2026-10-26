using NeetModKit.Features;
using PerformanceFixes.Navigation;
using UnityEngine.SceneManagement;

namespace PerformanceFixes.Features;

/// <summary>See <see cref="PathRequests"/>.</summary>
internal sealed class PathRequestsFeature : ModFeature
{
    public const string FeatureId = "PathRequests";

    public override string Id => FeatureId;
    public override string Name => "Characters plan their walk once";
    public override string Description =>
        "Characters walking somewhere (trick-or-treaters, the cat, rats, the delivery guy and others) plan their whole " +
        "path again on every frame. With this they only plan again when where they're going changes.";

    protected override void OnEnable() => SceneManager.sceneUnloaded += OnSceneUnloaded;

    protected override void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        PathRequests.Forget();
    }

    private static void OnSceneUnloaded(Scene scene) => PathRequests.Forget();
}
