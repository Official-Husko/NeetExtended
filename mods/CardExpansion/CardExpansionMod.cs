using CardExpansion.Cards;
using CardExpansion.Features;
using CardExpansion.UI;
using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Events;
using NeetModKit.Saves;
using NeetModKit.Settings;

namespace CardExpansion;

/// <summary>Entry point: wires the card set, the features, the UI and the save data together.</summary>
public sealed class CardExpansionMod : NeetMod
{
    private readonly Summoner _summoner = new Summoner();
    private readonly AutoClicker _autoClicker = new AutoClicker();
    private CardSiteUi _ui;

    internal static CardExpansionMod Instance { get; private set; }
    internal ConfigFile<CardExpansionConfig> Config { get; private set; }
    internal CardState State { get; private set; } = new CardState();

    public override void OnLoad()
    {
        Instance = this;
        Config = LoadConfig<CardExpansionConfig>();
        Config.Save();
        Config.Changed += _ => RefreshLabels();
        ModSettings.Register(this, Config, "Card Expansion");

        if (Config.Value.SpookyCards)
            SpookyCollection.Register(this);

        ModSaveData.Register(this, SaveState, LoadState);
        GameEvents.WhenHouseReady(this, OnHouseReady);
    }

    public override void OnUnload() => Instance = null;

    public override void OnUpdate()
    {
        if (GameEvents.IsHouseReady)
            _autoClicker.Tick(Config.Value, State);
    }

    private void OnHouseReady()
    {
        _autoClicker.RefreshCards();
        _ui = new CardSiteUi();
        if (!_ui.Build(this, Summon, ToggleAuto))
        {
            Log.Warn("Couldn't find the card site on the computer; buttons not added.");
            _ui = null;
            return;
        }
        RefreshLabels();
        _ui.SetStatus($"{State.Summons} summons, {State.Shinies} shiny");
    }

    private void Summon()
    {
        _ui?.SetStatus(_summoner.Summon(Config.Value, State));
    }

    private void ToggleAuto()
    {
        _ui?.SetStatus(_autoClicker.Buy(Config.Value, State));
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        var c = Config.Value;
        string auto = !State.AutoClickerOwned ? $"Auto\n{c.AutoClickerCost}*" : State.AutoClickerOn ? "Auto\nON" : "Auto\nOFF";
        _ui?.SetLabels($"Summon\n{c.SummonCost}*", auto);
    }

    private CardState SaveState()
    {
        State.CardBases = CardBaseValues.Capture();
        return State;
    }

    private void LoadState(CardState state)
    {
        State = state;
        CardBaseValues.Restore(state.CardBases);
        RefreshLabels();
    }
}
