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
        ModSaveData.Register(this, () => State, LoadState);

        ModFeatures.Register(this, new SpookyCardsFeature());
        ModFeatures.Register(this, _summons = new SummonFeature());
        ModFeatures.Register(this, _autoClicker = new AutoClickerFeature());
        ModFeatures.Register(this, new BetterExchangeFeature());
    }

    public override void OnUnload() => Instance = null;

    private void RefreshLabels()
    {
        _summons?.RefreshLabel();
        _autoClicker?.RefreshLabel();
    }

    private void LoadState(CardState state)
    {
        State = state;
        RefreshLabels();
    }
}
