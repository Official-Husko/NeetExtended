using NeetModKit.Features;
using UnityEngine;

namespace PerformanceFixes.Features;

/// <summary>See <see cref="Patches.IdlePaintPatches"/>.</summary>
internal sealed class IdlePaintFeature : ModFeature
{
    public const string FeatureId = "IdlePaint";

    public override string Id => FeatureId;
    public override string Name => "Idle paint surfaces rest";
    public override string Description =>
        "Every paintable surface in the house (over 1,200) checks for new paint on every frame. With this, a surface " +
        "only wakes up when something lands on it.";

    // Wake everything up again; surfaces only switch off through the patch.
    protected override void OnDisable()
    {
        foreach (var target in Resources.FindObjectsOfTypeAll<PaintTarget>())
        {
            if (target.gameObject.scene.IsValid() && !target.enabled)
                target.enabled = true;
        }
    }
}
