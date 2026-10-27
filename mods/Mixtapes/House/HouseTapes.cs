using System;
using System.Collections.Generic;
using Mixtapes.Tapes;
using NeetModKit.Events;
using NeetModKit.Game;
using NeetModKit.Saves;

namespace Mixtapes.House;

/// <summary>
/// Tapes with a "house" room are left in that room, one copy per save, the first time the house
/// loads with them (or right away when one is added while playing).
/// </summary>
internal static class HouseTapes
{
    internal sealed class State
    {
        public HashSet<string> Placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static State _state = new State();
    private static MixtapesMod _mod;

    public static void Init(MixtapesMod mod)
    {
        _mod = mod;
        ModSaveData.Register<State>(mod, () => _state, state => _state = state ?? new State());
        GameEvents.HouseReady.Subscribe(mod, PlaceNew);
        TapeLoader.Changed += PlaceIfInHouse;
        mod.Track(NeetLoader.Runtime.Disposable.Create(() => TapeLoader.Changed -= PlaceIfInHouse));
    }

    private static void PlaceIfInHouse()
    {
        if (GameEvents.IsHouseReady)
            PlaceNew();
    }

    private static void PlaceNew()
    {
        foreach (var tape in TapeLoader.Tapes)
        {
            if (tape.Room == null || _state.Placed.Contains(tape.ItemName) || !Spawning.IsItem(tape.ItemName))
                continue;
            try
            {
                Spawning.ItemInRoom(tape.ItemName, Rooms.Parse(tape.Room));
                _state.Placed.Add(tape.ItemName);
            }
            catch (ArgumentException e)
            {
                _mod.Log.Warn($"Couldn't leave {tape.ItemName} in {tape.Room}: {e.Message}");
            }
        }
    }
}
