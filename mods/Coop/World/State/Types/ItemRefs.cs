using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>Shared items that other shared items hold (a fuse in a radio, a magazine in a gun), by net ref.</summary>
internal static class ItemRefs
{
    public static NetRef Of(GameObject go)
    {
        var o = go != null ? NetObjects.Find(go) : null;
        return o != null ? o.Ref : default;
    }

    /// <summary>
    /// Known: the ref is "nothing", or a shared thing that's here (not one that hasn't arrived yet,
    /// whose holder is told again when it has).
    /// </summary>
    public static T Find<T>(NetRef r, out bool known) where T : Component
    {
        if (!r.IsValid)
        {
            known = true;
            return null;
        }
        var o = NetObjects.Find(r);
        known = o != null;
        return o != null ? o.Go.GetComponent<T>() : null;
    }
}
