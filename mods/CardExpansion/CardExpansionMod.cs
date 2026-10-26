using CardExpansion.Features;
using CardExpansion.UI;
using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Features;
using NeetModKit.Saves;
using NeetModKit.Settings;

namespace CardExpansion;

/// <summary>Entry point: settings, save data and the features (each can be switched on and off).</summary>
public sealed class CardExpansionMod : NeetMod
{
    private SummonFeature _summons;
    private AutoClickerFeature _autoClicker;
    private PromotionFixFeature _promotionFix;

    internal static CardExpansionMod Instance { get; private set; }
    internal ConfigFile<CardExpansionConfig> Config { get; private set; }
    internal CardState State { get; private set; } = new CardState();
    internal CardSiteUi SiteUi { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        SiteUi = new CardSiteUi(this);
        Config = LoadConfig<CardExpansionConfig>();
        Config.Save();
        Config.Changed += _ => RefreshLabels();
        ModSettings.Register(this, Config, "Card Expansion");
        ModSaveData.Register(this, SaveState, LoadState);

        ModFeatures.Register(this, new SpookyCardsFeature());
        ModFeatures.Register(this, _summons = new SummonFeature());
        ModFeatures.Register(this, _autoClicker = new AutoClickerFeature());
        ModFeatures.Register(this, new BetterExchangeFeature());
        ModFeatures.Register(this, _promotionFix = new PromotionFixFeature());
    }

    public override void OnUnload() => Instance = null;

    private void RefreshLabels()
    {
        _summons?.RefreshLabel();
        _autoClicker?.RefreshLabel();
    }

    private CardState SaveState()
    {
        State.CardBases = CardBaseValues.Capture();
        return State;
    }

    private void LoadState(CardState state)
    {
        State = state;
        if (_promotionFix == null || _promotionFix.Enabled)
            CardBaseValues.Restore(state.CardBases);
        RefreshLabels();
    }
}
