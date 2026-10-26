namespace NeetModKit.Features.Builtin;

internal sealed class NoFeedbackFeature : ModFeature
{
    public const string FeatureId = "NoFeedback";

    public override string Id => FeatureId;
    public override string Name => "No feedback reports";
    public override string Description =>
        "Stops the pause menu from sending feedback to the original developer. Reports from a modded game would describe code the developer never wrote.";
}
