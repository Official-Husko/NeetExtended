using System;
using System.Linq;
using NeetModKit.Game;
using NeetModKit.Ui.Toolkit;

namespace CheatMenu.Pages;

/// <summary>Spawn any item or person in front of the player.</summary>
internal sealed class SpawnPage : CheatPage
{
    private const int MaxResults = 30;

    private UiBuilder _results;
    private string _search = "";
    private int _count = 1;

    public override string Title => "Spawn";
    public override int Order => 20;

    protected override void BuildPage(UiBuilder ui)
    {
        var items = ui.Panel();
        items.Header("ITEMS");
        items.Input(_search, text =>
        {
            _search = text;
            FillResults();
        }, "Search items...");
        items.Slider("HOW MANY", _count, 1f, 10f, v => _count = (int)v, wholeNumbers: true);
        _results = items.Grid(200f, 26f);
        FillResults();

        var people = ui.Panel();
        people.Header("PEOPLE AND CREATURES");
        var grid = people.Grid(200f, 26f);
        foreach (string name in Spawning.NpcNames)
        {
            string npc = name;
            grid.Button(npc, () => Run(() => Spawning.Npc(npc, Spawning.InFrontOfPlayer(1.5f)), $"Spawned {npc}."), -1f);
        }
    }

    private void FillResults()
    {
        _results.Clear();
        var matches = Spawning.ItemNames
            .Where(n => _search.Length == 0 || n.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
            .Take(MaxResults);
        foreach (string name in matches)
        {
            string item = name;
            _results.Button(item, () => Run(() => SpawnItem(item), $"Spawned {_count} x {item}."), -1f);
        }
    }

    private void SpawnItem(string name)
    {
        for (int i = 0; i < _count; i++)
            Spawning.ItemNearPlayer(name);
    }
}
