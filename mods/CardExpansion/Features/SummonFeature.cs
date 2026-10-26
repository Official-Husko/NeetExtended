using NeetModKit.Events;
using NeetModKit.Features;
using NeetModKit.Ui;
using UnityEngine.UI;

namespace CardExpansion.Features;

internal sealed class SummonFeature : ModFeature
{
    private readonly Summoner _summoner = new Summoner();
    private Button _button;

    public override string Id => "Summons";
    public override string Name => "Summons";
    public override string Description => "A Summon button: spend crystals for a random card payout, or a shiny card with free levels.";

    private static CardExpansionMod Mod => CardExpansionMod.Instance;

    protected override void OnEnable() => GameEvents.WhenHouseReady(this, AddButton);

    public void RefreshLabel()
    {
        if (_button != null)
            UiCloning.SetText(_button.transform, $"Summon\n{Mod.Config.Value.SummonCost}*");
    }

    private void AddButton()
    {
        _button = Mod.SiteUi.AddButton(this, 0, "Summon", () => Mod.SiteUi.SetStatus(_summoner.Summon(Mod.Config.Value, Mod.State)));
        RefreshLabel();
        Mod.SiteUi.SetStatus($"{Mod.State.Summons} summons, {Mod.State.Shinies} shiny");
    }
}
