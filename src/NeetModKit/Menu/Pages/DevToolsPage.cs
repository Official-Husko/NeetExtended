using System;
using System.Linq;
using NeetModKit.Events;
using NeetModKit.Game;
using NeetModKit.Ui.Toolkit;
using UnityEngine.UI;

namespace NeetModKit.Menu.Pages;

/// <summary>Quick testing controls. Uses the same helpers as the dev bridge.</summary>
internal sealed class DevToolsPage : MenuPage
{
    private const int MaxSpawnResults = 24;
    private static readonly string[] Needs = { "Health", "Hunger", "Bladder", "Hygiene", "Energy" };

    private Text _money, _status, _room;
    private UiBuilder _results;
    private string _search = "";

    public override string Title => "Dev Tools";
    public override int Order => 40;

    public override void Build(UiBuilder ui)
    {
        var page = ui.Scroll(out _);
        if (!GameEvents.IsHouseReady)
        {
            page.Text("These tools work once you're in the house.");
            return;
        }

        _status = page.Text("", MenuPalette.Muted);
        BuildPlayer(page.Panel());
        BuildMoney(page.Panel());
        BuildSpawn(page.Panel());
        BuildHouse(page.Panel());
    }

    public override void Refresh()
    {
        if (_money == null || !GameEvents.IsHouseReady)
            return;
        _money.text = $"${Money.Get():0}";
        if (GameRefs.Player != null)
            _room.text = "YOU'RE IN: " + Rooms.At(GameRefs.Player.transform.position).ToString().ToUpperInvariant();
    }

    private void BuildPlayer(UiBuilder panel)
    {
        panel.Header("PLAYER");
        var row = panel.Row(8f, 26f);
        row.Toggle("God mode (no dying, needs frozen)", PlayerControl.GodMode, PlayerControl.SetGodMode);
        row.Button("FILL NEEDS", () => Try(() => { foreach (string n in Needs) PlayerControl.SetStat(n, 100f); }, "Needs filled."), 160f);

        panel.SubHeader("TELEPORT");
        var grid = panel.Grid(150f, 26f);
        foreach (var room in Rooms.All.Where(Rooms.Exists))
        {
            var target = room;
            grid.Button(room.ToString().ToUpperInvariant(), () => Try(() => PlayerControl.Teleport(target), $"Teleported to {target}."), -1f);
        }
    }

    private void BuildMoney(UiBuilder panel)
    {
        panel.Header("MONEY");
        var row = panel.Row(8f, 26f);
        _money = row.SubHeader("");
        UiBuilder.Size(_money, width: 120f);
        foreach (int amount in new[] { 100, 1000, 10000 })
            row.Button("+" + amount, () => Try(() => Money.Add(amount, taxed: false)), 100f);
        row.Button("CLEAR TAXES", () => Try(() => GameRefs.Computer.Taxes = 0f, "Taxes cleared."), 160f);
    }

    private void BuildSpawn(UiBuilder panel)
    {
        panel.Header("SPAWN AN ITEM");
        panel.Input(_search, text =>
        {
            _search = text;
            FillResults();
        }, "Search items...");
        _results = panel.Grid(200f, 26f);
        FillResults();
    }

    private void BuildHouse(UiBuilder panel)
    {
        panel.Header("HOUSE");
        var row = panel.Row(8f, 26f);
        _room = row.SubHeader("");
        UiBuilder.Size(_room, width: 260f);
        row.Button("START FIRE HERE", () => Try(() => HouseSystems.StartFire(CurrentRoom()), "Fire started. Good luck."), 180f, accent: true);
        row.Button("PUT FIRE OUT", () => Try(() => HouseSystems.StopFire(CurrentRoom()), "Fire's out."), 160f);

        var power = panel.Row(8f, 26f);
        power.Button("POWER ON", () => Try(() => HouseSystems.SetHousePower(true), "Power on everywhere."));
        power.Button("POWER OFF", () => Try(() => HouseSystems.SetHousePower(false), "Power off everywhere."));
    }

    private void FillResults()
    {
        _results.Clear();
        var matches = Spawning.ItemNames
            .Where(n => _search.Length == 0 || n.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
            .Take(MaxSpawnResults);
        foreach (string name in matches)
        {
            string item = name;
            _results.Button(item, () => Try(() => Spawning.ItemNearPlayer(item), $"Spawned {item}."), -1f);
        }
    }

    private static Location CurrentRoom() => Rooms.At(GameRefs.Player.transform.position);

    private void Try(Action action, string success = null)
    {
        try
        {
            action();
            if (success != null && _status != null)
                _status.text = success;
        }
        catch (Exception e)
        {
            if (_status != null)
                _status.text = MenuPalette.Paint("Didn't work: " + e.GetBaseException().Message, MenuPalette.Bad);
        }
    }
}
