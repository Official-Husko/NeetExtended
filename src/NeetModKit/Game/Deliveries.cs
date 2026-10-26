using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NeetModKit.Game;

/// <summary>
/// Packages dropped at the front door, the same way the computer's stores deliver orders
/// (<see cref="Computer.OrderItem"/>), but free and without the store checks.
/// </summary>
public static class Deliveries
{
    /// <summary>Drops a box holding one <paramref name="itemOrNpc"/> (an item or NPC prefab name) at the drop-off spot.</summary>
    public static GameObject Drop(string itemOrNpc)
    {
        var content = Spawning.IsItem(itemOrNpc) || !BrainBank.brainsDict.ContainsKey(itemOrNpc)
            ? Spawning.ItemPrefab(itemOrNpc)
            : Spawning.NpcPrefab(itemOrNpc);
        return Drop(content);
    }

    /// <summary>Drops a box holding <paramref name="content"/> (a prefab, not a scene object).</summary>
    public static GameObject Drop(GameObject content)
    {
        var computer = GameRefs.Computer ?? throw new InvalidOperationException("No computer (not in the house?).");
        var spot = GameRefs.House.packageDropOffPoint;
        if (spot == null)
            throw new InvalidOperationException("This house has no package drop-off point.");

        // Same placement as the game: a little to the side of the spot, falling in from above.
        var position = spot.position + spot.right * UnityEngine.Random.Range(-0.5f, 0f) + Vector3.up * 20f;
        var box = Object.Instantiate(computer.shippingBox, position, Quaternion.Euler(-90f, 0f, 0f));
        box.GetComponent<Cardboard_Box>().content = content;
        box.transform.localScale = BoxScale(content);
        return box;
    }

    /// <summary>The game sizes a box to fit its content's renderers (largest side first), with a 0.1 minimum.</summary>
    private static Vector3 BoxScale(GameObject content)
    {
        var size = Vector3.zero;
        foreach (var renderer in content.GetComponentsInChildren<MeshRenderer>())
            size = Vector3.Max(size, renderer.bounds.size);

        float largest = Mathf.Max(size.x, size.y, size.z);
        float middle = Mathf.Max(Mathf.Min(size.x, size.y), Mathf.Min(Mathf.Max(size.x, size.y), size.z));
        float smallest = Mathf.Min(size.x, size.y, size.z);
        return new Vector3(Mathf.Max(largest, 0.1f), Mathf.Max(middle, 0.1f), Mathf.Max(smallest, 0.1f)) * 2.5f;
    }
}
