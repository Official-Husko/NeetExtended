using NeetModKit.Features;

namespace CardExpansion.Features;

/// <summary>When on, card base values saved by the mod are put back after a load (see CardBaseValues).</summary>
internal sealed class PromotionFixFeature : ModFeature
{
    public override string Id => "PromotionFix";
    public override string Name => "Keep promoted card stats";
    public override string Description => "Fixes a game bug where promoted cards lose their doubled earnings after loading a save.";
    public override string AppliesWhen => "on the next load";
}
