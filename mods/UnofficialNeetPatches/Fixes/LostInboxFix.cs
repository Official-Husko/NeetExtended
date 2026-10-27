using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.LostInboxPatch"/>.</summary>
internal sealed class LostInboxFix : ModFeature
{
    public const string FeatureId = "LostInbox";

    public override string Id => FeatureId;
    public override string Name => "Emails stay in the inbox after loading";
    public override string Description =>
        "Loading a save keeps your emails, but the computer's inbox comes back empty: only emails that arrive " +
        "afterwards show up. With this, the inbox shows every email you had, read or not.";
}
