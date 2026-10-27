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
        ModFeatures.Register(this, new SaveCrashesFix());
        ModFeatures.Register(this, new LostCharactersFix());
        ModFeatures.Register(this, new StuckSequencesFix());
        ModFeatures.Register(this, new StatusTimersFix());
        ModFeatures.Register(this, new StuckSlowdownsFix());
        ModFeatures.Register(this, new EffectStrengthFix());
        ModFeatures.Register(this, new FrameRateSettingsFix());
        ModFeatures.Register(this, new LostInboxFix());
        ModFeatures.Register(this, new RepeatingSpamFix());
        ModFeatures.Register(this, new SpamTimerFix());
        ModFeatures.Register(this, new DoubleSpamFix());
        ModFeatures.Register(this, new KnotNamesFix());
        ModFeatures.Register(this, new SegmentLinesFix());
        ModFeatures.Register(this, new FailedOrderCopsFix());
        ModFeatures.Register(this, new CassettePriceFix());
        ModFeatures.Register(this, new LastMedalFix());
        ModFeatures.Register(this, new MoneyMedalFix());
        ModFeatures.Register(this, new WigPickupFix());
        ModFeatures.Register(this, new HiddenPickupsFix());
        ModFeatures.Register(this, new AutosaveAfterLoadFix());
        ModFeatures.Register(this, new SpawnersAfterLoadFix());
        ModFeatures.Register(this, new WornClothesFix());
        ModFeatures.Register(this, new MaskDeathFix());
        ModFeatures.Register(this, new MasksAfterLoadFix());
        ModFeatures.Register(this, new SkeletonPantsFix());
        ModFeatures.Register(this, new DeathDropsFix());
        ModFeatures.Register(this, new CameraPhotosFix());
        ModFeatures.Register(this, new LoadedPaintFix());
        ModFeatures.Register(this, new TvAfterLoadFix());
        ModFeatures.Register(this, new CustomShirtsFix());
        ModFeatures.Register(this, new PanCoolingFix());
        ModFeatures.Register(this, new ThunderLoopFix());
        var promotedCards = new PromotedCardsFix();
        ModFeatures.Register(this, promotedCards);
        ModSaveData.Register<PromotedCardsFix.SaveData>(this, promotedCards.Save, promotedCards.Load);
    }

    public override void OnUnload() => Instance = null;
}
