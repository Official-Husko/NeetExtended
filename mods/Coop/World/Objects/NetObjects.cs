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

    public static IReadOnlyList<NetObject> All => List;

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
    }

    public static NetObject Find(NetRef r) => ByRef.TryGetValue(r.Key, out var o) ? o : null;

    public static NetObject Find(GameObject go) =>
        go != null && ByGo.TryGetValue(go.GetInstanceID(), out var o) ? o : null;

    public static NetObject FindBody(Rigidbody body) =>
        body != null && ByBody.TryGetValue(body.GetInstanceID(), out var o) ? o : null;

    public static void Clear()
    {
        foreach (var o in List)
            o.Index = -1;
        List.Clear();
        ByRef.Clear();
        ByGo.Clear();
        ByBody.Clear();
    }
}
