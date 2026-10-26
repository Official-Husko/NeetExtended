using NeetModKit.Events;
using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="CharacterRegistry"/> and <see cref="Patches.CharacterRegistryPatches"/>.</summary>
internal sealed class LostCharactersFix : ModFeature
{
    public const string FeatureId = "LostCharacters";

    public override string Id => FeatureId;
    public override string Name => "Characters that come back";
    public override string Description =>
        "Once a character's first body is gone (killed, or removed by the story), story events can't find them anymore, " +
        "even if they're back. The thief's arrival then errors and repeats every frame, breaking the lounge window and " +
        "letting the thief in over and over. This keeps track of the character that's actually there.";

    protected override void OnEnable() => GameEvents.WhenHouseReady(this, CharacterRegistry.Repair);
}
