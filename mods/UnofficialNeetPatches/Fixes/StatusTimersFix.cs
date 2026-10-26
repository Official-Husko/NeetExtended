using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.StatusTimerPatches"/>.</summary>
internal sealed class StatusTimersFix : ModFeature
{
    public const string FeatureId = "StatusTimers";

    public override string Id => FeatureId;
    public override string Name => "Status effects end on time";
    public override string Description =>
        "Only the newest status effect counts down. Everything taken before it freezes: a pill or diarrhea keeps going " +
        "for as long as you crouch, or forever while you're heavy. With this, every effect runs for its own duration.";
}
