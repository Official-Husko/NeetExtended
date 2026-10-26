using System.Globalization;
using NeetModKit.Cards;
using NeetModKit.Game;
using NeetModKit.Ui.Toolkit;
using UnityEngine.UI;

namespace CheatMenu.Pages;

/// <summary>Money, taxes and the card game's crystals.</summary>
internal sealed class ComputerPage : CheatPage
{
    private Text _money, _taxes, _crystals;
    private string _amount = "1000";

    public override string Title => "Computer";
    public override int Order => 10;

    protected override void BuildPage(UiBuilder ui)
    {
        var money = ui.Panel();
        money.Header("MONEY");
        _money = money.SubHeader("");
        var add = money.Row(8f, 26f);
        foreach (int amount in new[] { 100, 1000, 10000, 100000 })
            add.Button("+$" + amount, () => Run(() => Money.Add(amount, taxed: false)), 120f);

        var set = money.Row(8f, 26f);
        set.Input(_amount, text => _amount = text, "Amount", 160f);
        set.Button("SET MONEY", () => Run(SetMoney, "Money set."), 160f);

        _taxes = money.SubHeader("");
        money.Button("CLEAR TAXES", () => Run(() => GameRefs.Computer.Taxes = 0f, "Taxes cleared."), 180f, accent: true);

        var cards = ui.Panel();
        cards.Header("ANIME CARDS");
        _crystals = cards.SubHeader("");
        var crystals = cards.Row(8f, 26f);
        foreach (int amount in new[] { 1000, 10000, 100000, 1000000 })
            crystals.Button("+" + amount + "*", () => Run(() => CardGame.Crystals += amount), 140f);
    }

    protected override void RefreshPage()
    {
        _money.text = $"CASH: ${Money.Get():0}";
        _taxes.text = GameRefs.Computer != null ? $"TAXES DUE: ${GameRefs.Computer.Taxes:0}" : "";
        _crystals.text = $"CRYSTALS: {CardGame.Crystals:0}";
    }

    private void SetMoney()
    {
        if (!float.TryParse(_amount, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            throw new System.ArgumentException("That isn't a number.");
        Money.SetUntaxed(value);
    }
}
