using Mixtapes.Radio;
using NeetModKit.Features;

namespace Mixtapes.Features;

/// <summary>The radio plays the mixtapes when there's no tape in it.</summary>
internal sealed class RadioStationFeature : ModFeature
{
    public const string FeatureId = "RadioStation";

    public override string Id => FeatureId;
    public override string Name => "Mixtape radio";
    public override string Description =>
        "With no tape in, the radio plays your mixtapes one after another, shuffled, like a station. " +
        "It still needs power (a fuse) and the music button pressed. Put a tape in and the tape plays.";

    protected override void OnEnable() => RadioStation.Enabled = true;

    protected override void OnDisable()
    {
        RadioStation.Enabled = false;
        RadioStation.StopAll();
    }
}
