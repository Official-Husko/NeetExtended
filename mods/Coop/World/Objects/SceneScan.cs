using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coop.World.Objects;

/// <summary>
/// Builds the registry from what's in the house right now (inactive things included). Used when world
/// sync starts and after a hot reload; spawn and destroy hooks keep it current in between.
/// </summary>
internal static class SceneScan
{
    public static void Rebuild()
    {
        NetObjects.Clear();
        LoadedIds.Ensure();
        var scene = SceneManager.GetActiveScene();
        int doors = 0, npcs = 0, items = 0, paths = 0, clashes = 0;

        foreach (var door in Object.FindObjectsOfType<Door>(true))
        {
            if (door.gameObject.scene == scene && Add(Classifier.ForDoor(door), ref clashes))
                doors++;
        }
        foreach (var brain in Object.FindObjectsOfType<Brain>(true))
        {
            if (brain.gameObject.scene == scene && Add(Classifier.ForBrain(brain), ref clashes))
                npcs++;
        }
        foreach (var usable in Object.FindObjectsOfType<Usable>(true))
        {
            if (usable.gameObject.scene != scene || NetObjects.Find(usable.gameObject) != null)
                continue;
            var o = Classifier.ForUsable(usable, scanning: true);
            if (!Add(o, ref clashes))
                continue;
            if (o.Ref.Kind == NetKind.Path)
                paths++;
            else
                items++;
        }
        CoopMod.Log.Info($"World: {items} items, {npcs} NPCs, {doors} doors and {paths} other scene objects are shared." +
            (clashes > 0 ? $" {clashes} left out (same id twice)." : ""));
    }

    private static bool Add(NetObject o, ref int clashes)
    {
        if (o == null)
            return false;
        if (!NetObjects.Add(o))
        {
            clashes++;
            var other = NetObjects.Find(o.Ref) ?? NetObjects.Find(o.Go);
            if (clashes <= 5)
                CoopMod.Log.Info($"World: {Describe(o.Go)} has the same id as {(other != null ? Describe(other.Go) : "?")} ({o.Ref}); it stays local.");
            return false;
        }
        LoadedIds.Add(o);
        return true;
    }

    private static string Describe(GameObject go) =>
        $"{ScenePath.Describe(go.transform)} ({(go.activeInHierarchy ? "active" : "inactive")})";
}
