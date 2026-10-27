using System.Collections.Generic;
using Coop.World.Objects;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coop.World.State;

/// <summary>
/// Every shared component in the house, found by net ref and type, or by the component itself (for
/// the hints). Scene things are found once per scan; components on items come and go with their item.
/// </summary>
internal static class StateTrackers
{
    private static readonly List<StateTracker> List = new List<StateTracker>(256);
    private static readonly Dictionary<(long, byte), StateTracker> ByKey = new Dictionary<(long, byte), StateTracker>(256);
    private static readonly Dictionary<int, StateTracker> ByComponent = new Dictionary<int, StateTracker>(256);

    public static IReadOnlyList<StateTracker> All => List;

    public static void Rebuild()
    {
        Clear();
        var scene = SceneManager.GetActiveScene();
        foreach (var type in StateTypes.All)
        {
            foreach (var found in Object.FindObjectsOfType(type.ComponentType, true))
            {
                if (found is Component component && component.gameObject.scene == scene)
                    Track(type, component);
            }
        }
    }

    /// <summary>A shared item appeared: track its shared components.</summary>
    public static void AddFor(NetObject o)
    {
        foreach (var type in StateTypes.All)
        {
            if (!type.OnItems)
                continue;
            var component = o.Go.GetComponentInChildren(type.ComponentType, true);
            if (component != null)
                Track(type, component);
        }
    }

    public static void RemoveFor(NetObject o)
    {
        for (int i = List.Count - 1; i >= 0; i--)
        {
            var t = List[i];
            if (t.Object != o)
                continue;
            ByKey.Remove(t.Key);
            ByComponent.Remove(t.ComponentId);
            List.RemoveAt(i);
        }
    }

    public static StateTracker Find(NetRef r, byte type) => ByKey.TryGetValue((r.Key, type), out var t) ? t : null;

    public static StateTracker Find(Component component) =>
        component != null && ByComponent.TryGetValue(component.GetInstanceID(), out var t) ? t : null;

    public static void Clear()
    {
        List.Clear();
        ByKey.Clear();
        ByComponent.Clear();
    }

    private static void Track(StateType type, Component component)
    {
        var o = NetObjects.Find(component.gameObject);
        // Some parts sit on a child of their item.
        if (o == null && type.OnItems)
        {
            var item = component.GetComponentInParent<Usable>();
            o = item != null ? NetObjects.Find(item.gameObject) : null;
        }
        NetRef r;
        if (o != null)
            r = o.Ref;
        else if (type.OnItems)
            return; // An item that isn't shared: its state isn't either.
        else if (component is ISaveableStatic saved)
            r = new NetRef(NetKind.Static, saved.SaveID);
        else
            r = new NetRef(NetKind.Path, ScenePath.Hash(component.transform));

        var t = new StateTracker { Ref = r, Type = type, Component = component, Object = o, ComponentId = component.GetInstanceID() };
        if (ByKey.ContainsKey(t.Key) || ByComponent.ContainsKey(t.ComponentId))
            return;
        List.Add(t);
        ByKey[t.Key] = t;
        ByComponent[t.ComponentId] = t;
        StateSync.Capture(t);
    }
}
