using NeetModKit.Features;

namespace OpenSaves.Features;

internal sealed class SaveCleanupFeature : ModFeature
{
    public const string FeatureId = "SaveCleanup";

    public override string Id => FeatureId;
    public override string Name => "Save cleanup";
    public override string Description =>
        "Keep only the newest saves (25 by default, see Settings) instead of a new file piling up every time the game saves.";
}
