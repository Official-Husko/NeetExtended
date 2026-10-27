using System.Collections.Generic;
using NeetModKit.Game;
using Randomizer.Seeds;
using UnityEngine;

namespace Randomizer.Parts;

/// <summary>
/// On a new game, loose items in the rooms you can reach from the start swap places. Keys, notes,
/// things fixed to walls, plugged-in parts and anything heavy stay where they are, and nothing is
/// moved into or out of a locked or walled-off room, so every save can still be finished.
/// </summary>
internal static class HouseShuffle
{
    // Rooms behind a locked door or a demo wall, outside, or not the house.
    private static readonly HashSet<Location> Excluded = new HashSet<Location>
    {
        Location.Basement_Backroom, Location.Attic, Location.Stairs_Attic, Location.Pantry,
        Location.Shed, Location.Backyard, Location.Other, Location.Dream,
    };

    private const float MaxMass = 30f;

    // The house spans about -4 (basement) to 12 (roof); the game parks worn clothes at -500.
    private const float Lowest = -10f;
    private const float Highest = 30f;

    public static bool Enabled { get; set; }

    /// <summary>Once per save, when the house of a new game is ready.</summary>
    public static void ApplyOnce()
    {
        var state = SeedState.Current;
        if (state.HouseDone)
            return;
        state.HouseDone = true;
        if (!Enabled)
            return;

        var items = Candidates();
        var spots = new List<(Vector3 Position, Quaternion Rotation)>();
        foreach (var item in items)
            spots.Add((item.transform.position, item.transform.rotation));
        Rolls.For(state.Seed, "house").Shuffle(spots);

        for (int i = 0; i < items.Count; i++)
        {
            items[i].transform.SetPositionAndRotation(spots[i].Position, spots[i].Rotation);
            var body = items[i].GetComponent<Rigidbody>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
        }
        RandomizerMod.Instance.Log.Info($"Shuffled {items.Count} items around the house.");
    }

    private static List<GameObject> Candidates()
    {
        var items = new List<GameObject>();
        foreach (var usable in Object.FindObjectsOfType<Usable>())
        {
            var go = usable.gameObject;
            var body = go.GetComponent<Rigidbody>();
            if (body == null || body.isKinematic || body.mass > MaxMass || go.GetComponent<FixedJoint>() != null)
                continue;
            if (usable.player != null || go.GetComponent<Key>() != null || go.name.StartsWith("Note", System.StringComparison.Ordinal))
                continue;
            var position = go.transform.position;
            if (position.y < Lowest || position.y > Highest)
                continue;
            if (usable.prefabName == null || !Spawning.IsItem(usable.prefabName) || Excluded.Contains(Rooms.At(position)))
                continue;
            items.Add(go);
        }
        return items;
    }
}
