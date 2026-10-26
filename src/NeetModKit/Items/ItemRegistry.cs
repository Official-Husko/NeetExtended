using System;
using System.Collections.Generic;
using NeetLoader;
using NeetLoader.Logging;
using NeetLoader.Runtime;
using NeetModKit.Game;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NeetModKit.Items;

/// <summary>
/// Adds new items to the game. Registered items can be spawned, bought (if given a store),
/// picked up, used and saved like the game's own. Removing the mod later is safe: saved copies
/// are skipped on load.
/// </summary>
public static class ItemRegistry
{
    private sealed class Entry
    {
        public ItemDefinition Definition;
        public GameObject Template;
    }

    private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private static ModLogger _log;

    public static IEnumerable<string> Names => Entries.Keys;

    public static bool IsModded(string name) => Entries.ContainsKey(name);

    /// <summary>The template copies are made from, once built (inside the house).</summary>
    public static GameObject Template(string name) => Entries.TryGetValue(name, out var e) ? e.Template : null;

    public static void Register(NeetMod owner, ItemDefinition def)
    {
        if (string.IsNullOrEmpty(def.Name) || string.IsNullOrEmpty(def.BasedOn))
            throw new ArgumentException("An item needs a Name and a BasedOn item.");
        if (Entries.ContainsKey(def.Name))
            throw new ArgumentException($"An item called '{def.Name}' is already registered.");

        var entry = new Entry { Definition = def };
        Entries[def.Name] = entry;
        owner.Track(Disposable.Create(() => Unregister(def.Name)));

        // The game's registry only exists in the house; otherwise wait for it.
        if (PickupBank.pickupsDict.Count > 0)
            Materialize(entry);
    }

    internal static void Init(ModLogger log) => _log = log;

    /// <summary>Called whenever the game (re)builds its item registry.</summary>
    internal static void RegisterAll()
    {
        foreach (var entry in Entries.Values)
            Materialize(entry);
    }

    private static void Materialize(Entry entry)
    {
        var def = entry.Definition;
        try
        {
            if (entry.Template == null)
                entry.Template = ItemTemplateBuilder.Build(def, Spawning.ItemPrefab(def.BasedOn));

            PickupBank.pickupsDict[def.Name] = new Spawnable(entry.Template);
            StoreListings.Add(def, entry.Template);
        }
        catch (Exception e)
        {
            _log.Error($"Couldn't create item '{def.Name}'", e);
        }
    }

    private static void Unregister(string name)
    {
        if (!Entries.TryGetValue(name, out var entry))
            return;
        Entries.Remove(name);
        PickupBank.pickupsDict.Remove(name);
        StoreListings.Remove(name);
        if (entry.Template != null)
            Object.Destroy(entry.Template);
    }
}
