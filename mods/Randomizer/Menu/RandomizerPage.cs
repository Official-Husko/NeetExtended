using System.Globalization;
using NeetModKit.Events;
using NeetModKit.Menu;
using NeetModKit.Ui.Toolkit;
using Randomizer.Parts;
using Randomizer.Seeds;
using UnityEngine.UI;

namespace Randomizer.Menu;

/// <summary>Mod menu tab: this save's seed, what it decided, and a button to roll again.</summary>
internal sealed class RandomizerPage : MenuPage
{
    private Text _status;

    public override string Title => "Randomizer";
    public override int Order => 34;

    public override void Build(UiBuilder ui)
    {
        ui.Text(MenuPalette.Paint(
            "Every save has a seed that decides who knocks when, what the store charges, what deliveries bring, " +
            "where things lie on a new game and how fast you get hungry. Switch the parts on the Mods page; " +
            "set a seed for new games in the settings.", MenuPalette.Muted));

        if (!GameEvents.IsHouseReady)
        {
            ui.Text("Load a game to see its seed.");
            return;
        }

        var top = ui.Row(8f, 26f);
        top.SubHeader("SEED  " + SeedState.Seed.ToString(CultureInfo.InvariantCulture));
        top.Button("ROLL AGAIN", () =>
        {
            RandomizerMod.Reroll();
            Rebuild();
            Report("New seed. Prices, needs and the visitors still to come changed; the house stays as it is.");
        }, 160f);
        _status = ui.Text("");

        var panel = ui.Panel();
        panel.SubHeader("VISITORS");
        panel.Text(Visitors.Enabled ? string.Join(", ", Visitors.Names()) : MenuPalette.Paint("In the game's order (Shuffled visitors is off).", MenuPalette.Muted));
        panel.SubHeader("NEEDS");
        panel.Text(Needs.Enabled
            ? $"Hunger {Percent(Needs.Speed("hunger"))}, energy {Percent(Needs.Speed("energy"))}, bladder {Percent(Needs.Speed("bladder"))}, hygiene {Percent(Needs.Speed("hygiene"))}."
            : MenuPalette.Paint("As in the game (Random needs is off).", MenuPalette.Muted));
    }

    // 1.25 -> "25% faster"
    private static string Percent(float speed)
    {
        int percent = (int)System.Math.Round((speed - 1f) * 100f);
        return percent == 0 ? "as usual" : percent > 0 ? $"{percent}% faster" : $"{-percent}% slower";
    }

    private void Report(string message)
    {
        if (_status != null)
            _status.text = message;
    }
}
