using NeetLoader;
using NeetModKit.Features;
using NeetModKit.Saves;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches;

/// <summary>
/// Fixes for the game's own bugs. Each fix is a feature with its own Harmony patch category, so it
/// can be switched off on the Mods page. Write-ups are in docs/game/known-bugs.md.
/// </summary>
public sealed class UnofficialNeetPatchesMod : NeetMod
{
    internal static UnofficialNeetPatchesMod Instance { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        ModFeatures.Register(this, new SpiderSavesFix());
        ModFeatures.Register(this, new GhostSavesFix());
        ModFeatures.Register(this, new LostCharactersFix());
        ModFeatures.Register(this, new StuckSequencesFix());
        ModFeatures.Register(this, new StatusTimersFix());
        ModFeatures.Register(this, new StuckSlowdownsFix());
        ModFeatures.Register(this, new EffectStrengthFix());
        var promotedCards = new PromotedCardsFix();
        ModFeatures.Register(this, promotedCards);
        ModSaveData.Register<PromotedCardsFix.SaveData>(this, promotedCards.Save, promotedCards.Load);
    }

    public override void OnUnload() => Instance = null;
}
