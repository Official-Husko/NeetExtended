using System.Collections.Generic;
using NeetModKit.Events;
using NeetModKit.Saves;
using UnityEngine;

namespace NeetModKit.Rules;

/// <summary>
/// Named numbers content can set and check ("pizza_list", "met_the_ghost"), shared by every mod, so
/// an email can set a flag a phone call checks. Kept in the save, along with the play clock.
/// </summary>
public static class ModFlags
{
    internal sealed class State
    {
        public Dictionary<string, int> Flags = new Dictionary<string, int>();
        public float PlaySeconds;
        public int Nights;
    }

    private static State _state = new State();
    private static float _clockBase;
    private static float _clockSince;

    // Values that older versions of mods kept themselves, handed over while a save loads.
    private static readonly Dictionary<string, int> Adopted = new Dictionary<string, int>();
    private static float _adoptedSeconds;
    private static bool _loading;

    public static int Get(string name) => name != null && _state.Flags.TryGetValue(name, out int value) ? value : 0;

    public static void Set(string name, int value)
    {
        if (string.IsNullOrEmpty(name))
            return;
        if (value == 0)
            _state.Flags.Remove(name);
        else
            _state.Flags[name] = value;
    }

    public static void Add(string name, int amount) => Set(name, Get(name) + amount);

    public static IReadOnlyDictionary<string, int> All => _state.Flags;

    /// <summary>
    /// Which day it is: 1 at the start, one more every time the player sleeps and wakes up again.
    /// (The game's own EncounterSpawner.nightId never changes.)
    /// </summary>
    public static int Day => _state.Nights + 1;

    /// <summary>Seconds the house was loaded in this save, across sessions.</summary>
    public static float PlaySeconds => _clockBase + (Time.time - _clockSince);

    /// <summary>
    /// Takes over flags and play time a mod kept in its own save data before they were shared (the
    /// phone did). Call it from that mod's save data loader; it merges into what this save has.
    /// </summary>
    public static void Adopt(IDictionary<string, int> flags, float playSeconds)
    {
        if (flags != null)
        {
            foreach (var pair in flags)
                Adopted[pair.Key] = pair.Value;
        }
        if (playSeconds > _adoptedSeconds)
            _adoptedSeconds = playSeconds;
        if (!_loading)
            ApplyAdopted();
    }

    internal static void Init(NeetLoader.NeetMod kit)
    {
        ModSaveData.Register<State>(kit, Snapshot, Restore, key: "flags");
        GameEvents.WokeUp.Subscribe(kit, () => _state.Nights++);
        // While a save loads, data arrives in no particular order: adopted values wait for this mod's own.
        GameEvents.BeforeLoad.Subscribe(kit, () =>
        {
            _loading = true;
            Adopted.Clear();
            _adoptedSeconds = 0f;
        });
    }

    private static State Snapshot()
    {
        _state.PlaySeconds = PlaySeconds;
        return _state;
    }

    private static void Restore(State state)
    {
        _state = state ?? new State();
        _clockBase = _state.PlaySeconds;
        _clockSince = Time.time;
        _loading = false;
        ApplyAdopted();
    }

    private static void ApplyAdopted()
    {
        foreach (var pair in Adopted)
        {
            if (!_state.Flags.ContainsKey(pair.Key))
                _state.Flags[pair.Key] = pair.Value;
        }
        Adopted.Clear();
        if (_adoptedSeconds > PlaySeconds)
        {
            _clockBase = _adoptedSeconds;
            _clockSince = Time.time;
        }
        _adoptedSeconds = 0f;
    }
}
