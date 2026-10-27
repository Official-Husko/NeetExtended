using Coop.World.Npcs;
using UnityEngine;

namespace Coop.World.Objects;

/// <summary>
/// Decides whether a game object is networked and how: its net ref, its body and whether it's an NPC.
/// Returns null for everything that stays local (players, levers and buttons, rig parts).
/// </summary>
internal static class Classifier
{
    public static NetObject ForDoor(Door door)
    {
        if (door == null)
            return null;
        var o = Make(door.gameObject, new NetRef(NetKind.Door, door.SaveID));
        o.Door = door;
        o.Usable = door.GetComponent<Usable>();
        return o;
    }

    public static NetObject ForBrain(Brain brain)
    {
        // Players are the lead's puppets; brains get their id in Start.
        if (brain == null || brain is BrainPlayer || brain.saveId == 0)
            return null;
        var o = Make(brain.gameObject, new NetRef(NetKind.Npc, brain.saveId));
        o.Brain = brain;
        o.Usable = brain.GetComponent<Usable>();
        o.PrefabName = brain.prefabName ?? "";
        o.Rig = NpcRig.TryCreate(brain);
        return o;
    }

    /// <summary>
    /// Items with a prefab name go by their save id. Scene objects without one go by their place in
    /// the scene, but only when <paramref name="scanning"/>: a copy made at runtime has no twin elsewhere.
    /// </summary>
    public static NetObject ForUsable(Usable usable, bool scanning)
    {
        if (usable == null || usable.lever || usable.button || usable.GetComponent<Rigidbody>() == null)
            return null;
        if (usable.GetComponent<Door>() != null || usable.GetComponent<Brain>() != null)
            return null;

        NetRef r;
        if (!string.IsNullOrEmpty(usable.prefabName))
        {
            if (usable.saveId <= 0)
                return null;
            r = new NetRef(NetKind.Item, usable.saveId);
        }
        else
        {
            // Bits of a character (cloth triggers and the like) belong to its rig.
            if (!scanning || usable.GetComponentInParent<Brain>(true) != null)
                return null;
            r = new NetRef(NetKind.Path, ScenePath.Hash(usable.transform));
        }
        var o = Make(usable.gameObject, r);
        o.Usable = usable;
        o.PrefabName = usable.prefabName ?? "";
        return o;
    }

    private static NetObject Make(GameObject go, NetRef r)
    {
        var o = new NetObject
        {
            Ref = r,
            Go = go,
            Tf = go.transform,
            Body = go.GetComponent<Rigidbody>(),
        };
        o.GameKinematic = o.Body != null && o.Body.isKinematic;
        o.SentPosition = o.Tf.position;
        o.SentRotation = o.Tf.rotation;
        o.SentScale = o.Tf.localScale;
        o.SentFlags = BodyFlags.Of(o);
        return o;
    }
}
