using System;
using System.Collections.Generic;
using System.Linq;
using NeetModKit.Events;
using NeetModKit.Game;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>Quick testing controls. Uses the same helpers as the dev bridge.</summary>
internal sealed class DevToolsPage : MenuPage
{
    private const int MaxSpawnResults = 24;
    private static readonly string[] Needs = { "Health", "Hunger", "Bladder", "Hygiene", "Energy" };

    private string[] _rooms = new string[0];
    private string _search = "";
    private string _searchedFor;
    private List<string> _matches = new List<string>();
    private string _status;

    public override string Title => "Dev Tools";
    public override int Order => 40;

    public override void OnShow()
    {
        _rooms = Rooms.All.Where(Rooms.Exists).Select(r => r.ToString()).ToArray();
        _searchedFor = null;
    }

    public override void Draw()
    {
        if (!GameEvents.IsHouseReady)
        {
            Widgets.Text("These tools work once you're in the house.");
            return;
        }

        Widgets.Section("Player", DrawPlayer);
        Widgets.Section("Money", DrawMoney);
        Widgets.Section("Spawn an item", DrawSpawn);
        Widgets.Section("House", DrawHouse);

        if (_status != null)
            Widgets.Small(_status);
    }

    private void DrawPlayer()
    {
        bool god = PlayerControl.GodMode;
        if (Widgets.Toggle("God mode (no dying, needs frozen)", god) != god)
            PlayerControl.SetGodMode(!god);

        if (Widgets.Button("Fill all needs", 160))
        {
            foreach (string need in Needs)
                Try(() => PlayerControl.SetStat(need, 100f));
            _status = "Needs filled.";
        }

        Widgets.Space(4);
        Widgets.Small("Teleport to a room:");
        int picked = GUILayout.SelectionGrid(-1, _rooms, 5, MenuStyles.Button);
        if (picked >= 0)
            Try(() => PlayerControl.Teleport(Rooms.Parse(_rooms[picked])), $"Teleported to {_rooms[picked]}.");
    }

    private void DrawMoney()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"${Money.Get():0}", MenuStyles.Header, GUILayout.Width(120));
        foreach (int amount in new[] { 100, 1000, 10000 })
        {
            if (Widgets.Button("+" + amount, 90))
                Try(() => Money.Add(amount, taxed: false));
        }
        if (Widgets.Button("Clear taxes", 120))
            Try(() => GameRefs.Computer.Taxes = 0f, "Taxes cleared.");
        GUILayout.EndHorizontal();
    }

    private void DrawSpawn()
    {
        _search = Widgets.TextField("Search", _search, 80);
        if (_search != _searchedFor)
        {
            _searchedFor = _search;
            _matches = Spawning.ItemNames
                .Where(n => _search.Length == 0 || n.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(MaxSpawnResults)
                .ToList();
        }

        int columns = 4;
        for (int i = 0; i < _matches.Count; i += columns)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Math.Min(i + columns, _matches.Count); j++)
            {
                string name = _matches[j];
                if (GUILayout.Button(name, MenuStyles.Button, GUILayout.Width(210)))
                    Try(() => Spawning.ItemNearPlayer(name), $"Spawned {name}.");
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawHouse()
    {
        var here = GameRefs.Player != null ? Rooms.At(GameRefs.Player.transform.position) : Location.Other;
        GUILayout.BeginHorizontal();
        GUILayout.Label($"You're in: {here}", MenuStyles.Text, GUILayout.Width(220));
        if (Widgets.Button("Start fire here", 150))
            Try(() => HouseSystems.StartFire(here), "Fire started. Good luck.");
        if (Widgets.Button("Put fire out", 130))
            Try(() => HouseSystems.StopFire(here), "Fire's out.");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (Widgets.Button("Power on", 130))
            Try(() => HouseSystems.SetHousePower(true), "Power on everywhere.");
        if (Widgets.Button("Power off", 130))
            Try(() => HouseSystems.SetHousePower(false), "Power off everywhere.");
        GUILayout.EndHorizontal();
    }

    private void Try(Action action, string success = null)
    {
        try
        {
            action();
            if (success != null)
                _status = success;
        }
        catch (Exception e)
        {
            _status = "Didn't work: " + e.GetBaseException().Message;
        }
    }
}
