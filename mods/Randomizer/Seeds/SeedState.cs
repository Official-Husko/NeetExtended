using System;
using System.Collections.Generic;
using NeetModKit.Events;
using NeetModKit.Saves;

namespace Randomizer.Seeds;

/// <summary>
/// The save's seed and what has been decided from it, kept in the save. A save without it (a new
/// game, or a save from before the mod) gets a seed the first time it loads.
/// </summary>
public static class SeedState
{
    internal sealed class State
    {
        public int Seed;
        public bool HasSeed;

        /// <summary>The house was shuffled, or this save was too far along for it when the mod first saw it.</summary>
        public bool HouseDone;

        /// <summary>Night 1's visitors as indexes into the game's list, in the order they come.</summary>
        public List<int> Visitors;

        /// <summary>Orders placed so far, so every order gets its own roll.</summary>
        public int Orders;
    }

    private static State _state = new State();
    private static bool _freshGame;

    internal static State Current => _state;

    public static int Seed => _state.Seed;

    /// <summary>Raised after a save's seed is known (each load, and for a new game).</summary>
    public static event Action Ready;

    internal static void Init(RandomizerMod mod)
    {
        // Before the save is read: is there one at all? No save file means a new game.
        GameEvents.BeforeLoad.Subscribe(mod, () => _freshGame = SaveFiles.Newest() == null);
        ModSaveData.Register<State>(mod, () => _state, Restore);
        // Installed or switched on in the middle of a game: that save gets its seed now.
        if (GameEvents.IsHouseReady && !_state.HasSeed)
            Restore(_state);
    }

    private static void Restore(State state)
    {
        _state = state ?? new State();
        if (!_state.HasSeed)
        {
            string configured = RandomizerMod.Config.Value.Seed;
            _state.Seed = string.IsNullOrWhiteSpace(configured) ? Environment.TickCount ^ Guid.NewGuid().GetHashCode() : Rolls.FromText(configured);
            _state.HasSeed = true;
            // Only a brand new game gets its house shuffled; an old save keeps where things are.
            _state.HouseDone = !_freshGame;
        }
        Ready?.Invoke();
    }
}
