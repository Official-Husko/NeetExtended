using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NeetModKit.Game;

/// <summary>
/// Spawning things from the game's registries: items (<see cref="PickupBank"/>) and NPCs
/// (<see cref="BrainBank"/>). Both registries are only filled inside the house scene.
/// </summary>
public static class Spawning
{
    public static IEnumerable<string> ItemNames => PickupBank.pickupsDict.Keys.OrderBy(k => k);

    public static IEnumerable<string> NpcNames => BrainBank.brainsDict.Keys.OrderBy(k => k);

    public static bool IsItem(string name) => PickupBank.pickupsDict.ContainsKey(name);

    public static GameObject ItemPrefab(string name) =>
        PickupBank.pickupsDict.TryGetValue(name, out var s) ? s.obj : throw Unknown("item", name, ItemNames);

    public static GameObject NpcPrefab(string name) =>
        BrainBank.brainsDict.TryGetValue(name, out var s) ? s.obj : throw Unknown("NPC", name, NpcNames);

    /// <summary>Spawns an item where the player is looking, like the console's "get" command.</summary>
    public static GameObject ItemNearPlayer(string name) => Object.Instantiate(ItemPrefab(name), InFrontOfPlayer(), Quaternion.identity);

    /// <summary>Spawns an item in a room and gives it a little push, like the console's "spawnAt".</summary>
    public static GameObject ItemInRoom(string name, Location room)
    {
        var center = Rooms.Center(room) ?? throw new ArgumentException($"{room} isn't in this scene.");
        var go = Object.Instantiate(ItemPrefab(name), center, Quaternion.identity);
        var body = go.GetComponent<Rigidbody>();
        if (body != null)
            body.AddForce(UnityEngine.Random.insideUnitSphere * body.mass);
        return go;
    }

    public static GameObject Item(string name, Vector3 position) => Object.Instantiate(ItemPrefab(name), position, Quaternion.identity);

    public static GameObject Npc(string name, Vector3 position) => Object.Instantiate(NpcPrefab(name), position, Quaternion.identity);

    public static GameObject NpcInRoom(string name, Location room) =>
        Npc(name, Rooms.FloorPoint(room) ?? throw new ArgumentException($"{room} isn't in this scene."));

    public static Vector3 InFrontOfPlayer(float distance = 0.6f)
    {
        var cam = Camera.main;
        if (cam == null)
            throw new InvalidOperationException("No camera right now.");
        var anchor = cam.transform.parent != null ? cam.transform.parent : cam.transform;
        return anchor.position + cam.transform.forward * distance;
    }

    private static ArgumentException Unknown(string kind, string name, IEnumerable<string> known)
    {
        var close = known.Where(k => k.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).Take(8).ToList();
        string hint = close.Count > 0 ? " Did you mean: " + string.Join(", ", close) : "";
        return new ArgumentException($"No {kind} called '{name}'.{hint}");
    }
}
