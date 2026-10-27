using System.Collections.Generic;
using Coop.World.Objects;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coop.World.Flames;

/// <summary>
/// Which flames are shared and how they're named. An item's flames go by the item's ref and their
/// place among its flames; a flame of the scene (not on an item) by where it sits in the scene.
/// Left out, because they are shared some other way: the room fire's flames (FireSync), the stove's
/// (GasSync) and the heat of pans and pots (their HeatHolder, which is state: HeatState).
/// A spray's fireball isn't an item's flame either: the game that made it tells the others (FireballSync).
/// </summary>
internal static class FlameBook
{
    private static readonly Dictionary<int, FlameInfo> ById = new Dictionary<int, FlameInfo>(64);
    private static readonly Dictionary<long, FlameInfo> ByKey = new Dictionary<long, FlameInfo>(64);
    private static readonly HashSet<int> Ignored = new HashSet<int>();
    private static readonly List<Flame> Buffer = new List<Flame>(8);

    // Only the flames found while the house is scanned count as the house's own.
    private static bool _scanning;

    public static IEnumerable<FlameInfo> All => ById.Values;

    public static void Clear()
    {
        ById.Clear();
        ByKey.Clear();
        Ignored.Clear();
    }

    /// <summary>The flames of the house itself, once, when world sync starts.</summary>
    public static void Rebuild()
    {
        Clear();
        var scene = SceneManager.GetActiveScene();
        _scanning = true;
        try
        {
            foreach (var flame in Object.FindObjectsOfType<Flame>(true))
            {
                if (flame.gameObject.scene == scene)
                    Info(flame);
            }
        }
        finally
        {
            _scanning = false;
        }
    }

    /// <summary>
    /// The shared flame behind a component, or null when it isn't one. The first look at a flame
    /// notes its state as it is, so call this before something changes it.
    /// </summary>
    public static FlameInfo Info(Flame flame) => flame == null ? null : Find(flame, false);

    /// <summary>A flame an item just caught (nothing was sent for it yet).</summary>
    public static FlameInfo Added(Flame flame) => flame == null ? null : Find(flame, true);

    /// <summary>The flame another game named, or null.</summary>
    public static FlameInfo Find(NetRef r, byte index)
    {
        if (ByKey.TryGetValue(r.Key * 256 + index, out var known) && known.Flame != null)
            return known;
        var o = NetObjects.Find(r);
        if (o == null || !o.Alive)
            return null;
        o.Go.GetComponentsInChildren<Flame>(true, Buffer);
        var flame = index < Buffer.Count ? Buffer[index] : null;
        Buffer.Clear();
        return flame != null ? Info(flame) : null;
    }

    public static byte Pack(Flame flame, bool runtime)
    {
        int heat = (int)flame._heatLevel;
        return (byte)(flame.lit ? 1 | (runtime ? 2 : 0) | (heat << 2) : 0);
    }

    public static bool IsLit(byte packed) => (packed & 1) != 0;

    public static bool IsRuntime(byte packed) => (packed & 2) != 0;

    public static HeatLevel Heat(byte packed) => (HeatLevel)(packed >> 2);

    public static void Forget(FlameInfo info)
    {
        ById.Remove(info.Id);
        ByKey.Remove(info.Key);
    }

    private static FlameInfo Find(Flame flame, bool fresh)
    {
        int id = flame.GetInstanceID();
        if (ById.TryGetValue(id, out var info))
            return info;
        if (Ignored.Contains(id))
            return null;
        if (flame.TryGetComponent(out FireParticle _))
            return null;
        if (flame.TryGetComponent(out HeatHolder _) || GasSync.IsStove(flame))
        {
            Ignored.Add(id);
            return null;
        }
        info = Make(flame, fresh);
        if (info == null)
            return null;
        ById[id] = info;
        ByKey[info.Key] = info;
        return info;
    }

    private static FlameInfo Make(Flame flame, bool fresh)
    {
        var item = flame.GetComponentInParent<Usable>(true);
        var o = item != null ? NetObjects.Find(item.gameObject) : null;
        var info = new FlameInfo { Flame = flame, Id = flame.GetInstanceID(), Runtime = fresh || flame.runtimeAddedFlame };
        if (o != null)
        {
            o.Go.GetComponentsInChildren<Flame>(true, Buffer);
            int at = Buffer.IndexOf(flame);
            Buffer.Clear();
            if (at < 0)
                return null;
            info.Ref = o.Ref;
            info.Index = (byte)at;
        }
        else
        {
            // Flames made while the game runs (a spray's fireball) have no twin in the other games.
            if (!_scanning)
                return null;
            info.Scene = true;
            info.Ref = new NetRef(NetKind.Path, ScenePath.Hash(flame.transform));
        }
        info.Last = fresh ? (byte)0 : Pack(flame, info.Runtime);
        return info;
    }
}
