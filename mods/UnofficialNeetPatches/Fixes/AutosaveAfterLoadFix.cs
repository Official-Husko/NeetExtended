using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.AutosaveAfterLoadPatch"/>.</summary>
internal sealed class AutosaveAfterLoadFix : ModFeature
{
    public const string FeatureId = "AutosaveAfterLoad";

    public override string Id => FeatureId;
    public override string Name => "Autosave works after loading";
    public override string Description =>
        "Once the game had loaded a save it never autosaved again for the rest of the session. " +
        "Now it saves every five minutes while you are in your bedroom, as it does in a fresh game.";
}
