using System.Collections.Generic;

namespace Coop.World.Objects;

/// <summary>
/// The game's own id lookups (SaveLoadHandler.loadedUsablesById / loadedBrainsById). Several of its
/// Load methods resolve ids through them, and World reuses those for spawned items, so they're kept
/// current here. They are null when the house started without a save file.
/// </summary>
internal static class LoadedIds
{
    public static void Ensure()
    {
        SaveLoadHandler.loadedUsablesById ??= new Dictionary<int, UnityEngine.GameObject>();
        SaveLoadHandler.loadedBrainsById ??= new Dictionary<int, UnityEngine.GameObject>();
    }

    public static void Add(NetObject o)
    {
        Ensure();
        if (o.Ref.Kind == NetKind.Item)
            SaveLoadHandler.loadedUsablesById[o.Ref.Id] = o.Go;
        else if (o.Ref.Kind == NetKind.Npc)
            SaveLoadHandler.loadedBrainsById[o.Ref.Id] = o.Go;
    }

    public static void Remove(NetObject o)
    {
        if (o.Ref.Kind == NetKind.Item)
            Drop(SaveLoadHandler.loadedUsablesById, o);
        else if (o.Ref.Kind == NetKind.Npc)
            Drop(SaveLoadHandler.loadedBrainsById, o);
    }

    private static void Drop(Dictionary<int, UnityEngine.GameObject> map, NetObject o)
    {
        if (map != null && map.TryGetValue(o.Ref.Id, out var go) && (go == null || go == o.Go))
            map.Remove(o.Ref.Id);
    }
}
