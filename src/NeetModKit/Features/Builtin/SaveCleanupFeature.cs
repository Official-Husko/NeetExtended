namespace NeetModKit.Features.Builtin;

internal sealed class SaveCleanupFeature : ModFeature
{
    public const string FeatureId = "SaveCleanup";

    public override string Id => FeatureId;
    public override string Name => "Save cleanup";
    public override string Description =>
        "The game writes a new save file every time it saves and never deletes any. This keeps the newest ones (25 by default, see Settings) and deletes the rest.";
}
