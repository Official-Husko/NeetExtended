using NeetModKit.Events;
using NeetModKit.Features;
using NeetModKit.Ui;
using UnityEngine.UI;

namespace CardExpansion.Features;

internal sealed class AutoClickerFeature : ModFeature
{
    private readonly AutoClicker _clicker = new AutoClicker();
    private Button _button;

    public override string Id => "AutoClicker";
    public override string Name => "Auto-clicker";
    public override string Description => "An auto-clicker you buy once with crystals; it clicks every unlocked card for you.";

    private static CardExpansionMod Mod => CardExpansionMod.Instance;

    protected override void OnEnable() => GameEvents.WhenHouseReady(this, () =>
    {
        _clicker.RefreshCards();
        _button = Mod.SiteUi.AddButton(this, 1, "Auto", () =>
        {
            Mod.SiteUi.SetStatus(_clicker.Buy(Mod.Config.Value, Mod.State));
            RefreshLabel();
        });
        RefreshLabel();
    });

    public override void OnUpdate()
    {
        if (GameEvents.IsHouseReady)
            _clicker.Tick(Mod.Config.Value, Mod.State);
    }

    public void RefreshLabel()
    {
        if (_button == null)
            return;
        var state = Mod.State;
        string label = !state.AutoClickerOwned ? $"Auto\n{Mod.Config.Value.AutoClickerCost}*" : state.AutoClickerOn ? "Auto\nON" : "Auto\nOFF";
        UiCloning.SetText(_button.transform, label);
    }
}
