using System.Collections.Generic;
using Randomizer.Seeds;

namespace Randomizer.Parts;

/// <summary>
/// Night 1's visitors come in a shuffled order. Visitors that already came (or are at the door) keep
/// their place, and follow-up visits (GhostBoy coming back to say the demo ends) stay where they are.
/// The game rebuilds its visitor list on every house load, so this is applied every time.
/// </summary>
internal static class Visitors
{
    public static bool Enabled { get; set; }

    public static void Apply()
    {
        var spawner = EncounterSpawner._;
        var original = spawner != null ? spawner.Night1Encounters : null;
        if (original == null || original.Length == 0 || EncounterSpawner.Nights == null)
            return;
        if (!Enabled)
        {
            EncounterSpawner.Nights[0] = original;
            return;
        }

        var state = SeedState.Current;
        if (state.Visitors == null || state.Visitors.Count != original.Length)
            state.Visitors = Order(original, FirstOpen(spawner), state.Seed);
        var shuffled = new EncounterSpawner.Encounter[original.Length];
        for (int i = 0; i < shuffled.Length; i++)
            shuffled[i] = original[state.Visitors[i]];
        EncounterSpawner.Nights[0] = shuffled;
    }

    /// <summary>The visitors in the order they come, by name, for the menu.</summary>
    public static List<string> Names()
    {
        var names = new List<string>();
        var night = EncounterSpawner.Nights?[0];
        if (night == null)
            return names;
        foreach (var encounter in night)
        {
            string name = encounter?.character != null ? encounter.character.name : "?";
            names.Add(string.IsNullOrEmpty(encounter?.specifiedStitch) ? name : name + " (again)");
        }
        return names;
    }

    /// <summary>Shuffles the visitors still to come again, from the (new) seed. The ones that came keep their place.</summary>
    public static void Reroll()
    {
        var spawner = EncounterSpawner._;
        if (spawner?.Night1Encounters == null)
        {
            SeedState.Current.Visitors = null;
            return;
        }
        SeedState.Current.Visitors = Order(spawner.Night1Encounters, FirstOpen(spawner), SeedState.Seed);
        Apply();
    }

    // encounterId is 1-based and points at the next visitor, or the one at the door.
    private static int FirstOpen(EncounterSpawner spawner) =>
        spawner.characterPresent ? EncounterSpawner.encounterId : System.Math.Max(0, EncounterSpawner.encounterId - 1);

    private static List<int> Order(EncounterSpawner.Encounter[] night, int firstOpen, int seed)
    {
        var order = new List<int>();
        var current = SeedState.Current.Visitors;
        for (int i = 0; i < night.Length; i++)
            order.Add(current != null && i < current.Count ? current[i] : i);

        var open = new List<int>();
        for (int i = firstOpen; i < night.Length; i++)
        {
            if (string.IsNullOrEmpty(night[order[i]].specifiedStitch))
                open.Add(i);
        }
        var picks = new List<int>();
        foreach (int slot in open)
            picks.Add(order[slot]);
        Rolls.For(seed, "visitors").Shuffle(picks);
        for (int k = 0; k < open.Count; k++)
            order[open[k]] = picks[k];
        return order;
    }
}
