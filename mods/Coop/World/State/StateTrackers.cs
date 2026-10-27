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
    private static readonly Dictionary<NetObject, List<StateTracker>> ByObject = new Dictionary<NetObject, List<StateTracker>>(256);

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

    /// <summary>
    /// A shared item appeared: track its shared components. <paramref name="fresh"/> is for an item made
    /// in this game: what it holds beyond its saved parts isn't known to the others, so the first look
    /// sends whatever isn't the resting (all zero) state.
    /// </summary>
    public static void AddFor(NetObject o, bool fresh = false)
    {
        foreach (var type in StateTypes.All)
        {
            if (!type.OnItems)
                continue;
            var component = o.Go.GetComponentInChildren(type.ComponentType, true);
            if (component != null)
                Track(type, component, fresh);
        }
    }

    public static void RemoveFor(NetObject o)
    {
        if (!ByObject.TryGetValue(o, out var mine))
            return;
        foreach (var t in mine)
        {
            ByKey.Remove(t.Key);
            ByComponent.Remove(t.ComponentId);
            List.Remove(t);
        }
        ByObject.Remove(o);
    }

    /// <summary>The trackers on one thing (null if it has none).</summary>
    public static List<StateTracker> Of(NetObject o) => ByObject.TryGetValue(o, out var mine) ? mine : null;

    public static StateTracker Find(NetRef r, byte type) => ByKey.TryGetValue((r.Key, type), out var t) ? t : null;

    public static StateTracker Find(Component component) =>
        component != null && ByComponent.TryGetValue(component.GetInstanceID(), out var t) ? t : null;

    public static void Clear()
    {
        List.Clear();
        ByKey.Clear();
        ByComponent.Clear();
        ByObject.Clear();
    }

    private static void Track(StateType type, Component component, bool fresh = false)
    {
        if (!type.Accepts(component))
            return;
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
        if (o != null)
        {
            if (!ByObject.TryGetValue(o, out var mine))
                ByObject[o] = mine = new List<StateTracker>(4);
            mine.Add(t);
        }
        if (fresh)
            StateSync.MarkFresh(t);
        else
            StateSync.Capture(t);
    }
}
