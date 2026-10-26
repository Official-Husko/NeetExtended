using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.TimerSequencePatch"/>.</summary>
internal sealed class StuckSequencesFix : ModFeature
{
    public const string FeatureId = "StuckSequences";

    public override string Id => FeatureId;
    public override string Name => "Story events that go wrong";
    public override string Description =>
        "When a timed story event runs into an error, the game tries it again every frame, forever, repeating whatever " +
        "it managed to do before the error. This lets it fail once and stop.";
}
