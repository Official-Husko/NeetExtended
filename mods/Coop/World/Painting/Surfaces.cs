using UnityEngine;

namespace Coop.World.Painting;

/// <summary>
/// The house's paint surfaces by their save id (<c>PaintTarget.saveId</c>), which is set in the scene,
/// so it's the same in every game and is what the join snapshot's paint pictures are keyed by. Paint
/// on anything that isn't one of these (an item's own surface) has no shared name and isn't sent.
/// </summary>
internal static class Surfaces
{
    private static PaintTarget[] _byId = new PaintTarget[0];
    private static HouseManager _house;

    public static void Rebuild()
    {
        _house = HouseManager._;
        var list = _house != null ? _house.paintTargets : null;
        if (list == null)
        {
            _byId = new PaintTarget[0];
            return;
        }
        int max = -1;
        foreach (var target in list)
        {
            if (target != null && target.saveId > max)
                max = target.saveId;
        }
        _byId = new PaintTarget[Mathf.Clamp(max + 1, 0, ushort.MaxValue + 1)];
        foreach (var target in list)
        {
            if (target != null && target.saveId >= 0 && target.saveId < _byId.Length)
                _byId[target.saveId] = target;
        }
    }

    public static void Clear()
    {
        _byId = new PaintTarget[0];
        _house = null;
    }

    /// <summary>The surface with this id, or null.</summary>
    public static PaintTarget Find(int id)
    {
        Check();
        var target = id >= 0 && id < _byId.Length ? _byId[id] : null;
        return target != null ? target : null;
    }

    /// <summary>True when the surface is one of the house's own, so others can find it by id.</summary>
    public static bool IsShared(PaintTarget target)
    {
        Check();
        int id = target.saveId;
        return id >= 0 && id < _byId.Length && ReferenceEquals(_byId[id], target);
    }

    // A new house (a load on the host) brings new surfaces under the same ids.
    private static void Check()
    {
        if (!ReferenceEquals(_house, HouseManager._))
            Rebuild();
    }
}
