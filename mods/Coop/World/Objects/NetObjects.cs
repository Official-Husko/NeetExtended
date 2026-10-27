using System.Collections.Generic;
using UnityEngine;

namespace Coop.World.Objects;

/// <summary>
/// Every networked thing in this game's house, found by net ref, by GameObject and by Rigidbody.
/// Built by <see cref="SceneScan"/> and kept current by the spawn and destroy hooks.
/// </summary>
internal static class NetObjects
{
    private static readonly List<NetObject> List = new List<NetObject>(1024);
    private static readonly Dictionary<long, NetObject> ByRef = new Dictionary<long, NetObject>(1024);
    private static readonly Dictionary<int, NetObject> ByGo = new Dictionary<int, NetObject>(1024);
    private static readonly Dictionary<int, NetObject> ByBody = new Dictionary<int, NetObject>(1024);
    private static readonly List<NetObject> MovingList = new List<NetObject>(128);
    private static readonly List<NetObject> RigList = new List<NetObject>(32);

    public static IReadOnlyList<NetObject> All => List;

    /// <summary>NPC rigs (the host poses them).</summary>
    public static IReadOnlyList<NetObject> Rigs => RigList;

    /// <summary>
    /// Bodies that moved lately, are held, or were just taken over: what the per-tick work walks.
    /// Entries that stopped (or went away) are dropped by whoever walks it next.
    /// </summary>
    public static List<NetObject> Moving => MovingList;

    public static void MarkMoving(NetObject o, float now)
    {
        o.Moving = true;
        o.LastChange = now;
        if (o.InMoving)
            return;
        o.InMoving = true;
        MovingList.Add(o);
    }

    /// <summary>Takes entry <paramref name="index"/> off the moving list (swap with the last).</summary>
    public static void DropMoving(int index)
    {
        MovingList[index].InMoving = false;
        int last = MovingList.Count - 1;
        MovingList[index] = MovingList[last];
        MovingList.RemoveAt(last);
    }

    public static int Count => List.Count;

    public static bool Add(NetObject o)
    {
        o.GoId = o.Go.GetInstanceID();
        o.BodyId = o.Body != null ? o.Body.GetInstanceID() : 0;
        if (ByRef.ContainsKey(o.Ref.Key) || ByGo.ContainsKey(o.GoId))
            return false;
        o.Index = List.Count;
        List.Add(o);
        ByRef[o.Ref.Key] = o;
        ByGo[o.GoId] = o;
        if (o.BodyId != 0)
            ByBody[o.BodyId] = o;
        if (o.Rig != null)
            RigList.Add(o);
        return true;
    }

    public static void Remove(NetObject o)
    {
        if (o.Index < 0 || o.Index >= List.Count || List[o.Index] != o)
            return;
        int last = List.Count - 1;
        var moved = List[last];
        List[o.Index] = moved;
        moved.Index = o.Index;
        List.RemoveAt(last);
        o.Index = -1;
        ByRef.Remove(o.Ref.Key);
        ByGo.Remove(o.GoId);
        if (o.BodyId != 0)
            ByBody.Remove(o.BodyId);
        if (o.Rig != null)
            RigList.Remove(o);
    }

    public static NetObject Find(NetRef r) => ByRef.TryGetValue(r.Key, out var o) ? o : null;

    public static NetObject Find(GameObject go) =>
        go != null && ByGo.TryGetValue(go.GetInstanceID(), out var o) ? o : null;

    public static NetObject FindBody(Rigidbody body) =>
        body != null && ByBody.TryGetValue(body.GetInstanceID(), out var o) ? o : null;

    public static void Clear()
    {
        foreach (var o in List)
        {
            o.Index = -1;
            o.InMoving = false;
        }
        List.Clear();
        MovingList.Clear();
        RigList.Clear();
        ByRef.Clear();
        ByGo.Clear();
        ByBody.Clear();
    }
}
