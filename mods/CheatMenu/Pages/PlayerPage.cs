using System.Collections.Generic;
using System.Linq;
using NeetModKit.Game;
using NeetModKit.Ui.Toolkit;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CheatMenu.Pages;

internal sealed class PlayerPage : CheatPage
{
    private static readonly string[] Needs = { "Health", "Hunger", "Bladder", "Hygiene", "Energy" };
    private readonly Dictionary<string, Slider> _sliders = new Dictionary<string, Slider>();

    public override string Title => "Player";
    public override int Order => 0;

    protected override void BuildPage(UiBuilder ui)
    {
        var flags = ui.Panel();
        flags.Header("SURVIVAL");
        var row = flags.Row(10f, 26f);
        row.Toggle("God mode (no dying)", PlayerControl.GodMode, on => Run(() => PlayerControl.SetGodMode(on)));
        row.Toggle("Needs don't drain", PlayerControl.NeedsFrozen, on => Run(() => PlayerControl.SetNeedsFrozen(on)));
        row.Button("FILL ALL", () => Run(FillNeeds, "Everything topped up."), 140f, accent: true);

        var needs = ui.Panel();
        needs.Header("NEEDS");
        _sliders.Clear();
        foreach (string need in Needs)
        {
            string stat = need;
            _sliders[stat] = needs.Slider(stat.ToUpperInvariant(), 0f, 0f, 100f, v => Run(() => PlayerControl.SetStat(stat, v)), wholeNumbers: true);
        }

        var movement = ui.Panel();
        movement.Header("MOVEMENT");
        movement.Slider("WALK SPEED", PlayerControl.SpeedMultiplier, 0.5f, 5f, v => Run(() => PlayerControl.SetSpeedMultiplier(v)));
        movement.SubHeader("TELEPORT");
        var grid = movement.Grid(150f, 26f);
        foreach (var room in Rooms.All.Where(Rooms.Exists))
        {
            var target = room;
            grid.Button(room.ToString().ToUpperInvariant(), () => Run(() => PlayerControl.Teleport(target), $"Teleported to {target}."), -1f);
        }
    }

    protected override void RefreshPage()
    {
        var stats = PlayerControl.ReadStats();
        var dragging = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        foreach (var pair in _sliders)
        {
            // Don't fight the player while they drag a slider.
            if (pair.Value != null && pair.Value.gameObject != dragging && stats.TryGetValue(pair.Key, out float value))
                pair.Value.SetValueWithoutNotify(value);
        }
    }

    private static void FillNeeds()
    {
        foreach (string need in Needs)
            PlayerControl.SetStat(need, 100f);
    }
}
