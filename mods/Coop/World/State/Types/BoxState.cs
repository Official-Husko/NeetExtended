using System;
using System.Collections.Generic;
using Coop.World.Objects;
using HarmonyLib;
using LiteNetLib.Utils;

namespace Coop.World.State.Types;

/// <summary>
/// A cardboard box: taped, opened, never opened, still holding its delivery. Opening and closing go
/// through the box's own Use, which also freezes what's inside to it (or lets it go), with its
/// delivery taken out first: the player who opened it unpacked it, and it arrives as a spawn.
/// </summary>
internal sealed class BoxState : StateType<Cardboard_Box>
{
    private static readonly Action<Cardboard_Box> BreakTape =
        AccessTools.MethodDelegate<Action<Cardboard_Box>>(AccessTools.Method(typeof(Cardboard_Box), "BreakTape"));

    private static readonly List<NetObject> Inside = new List<NetObject>();

    public override bool OnItems => true;

    protected override void Write(Cardboard_Box box, NetDataWriter w)
    {
        w.Put(box.opened);
        w.Put(box.taped);
        w.Put(box.virgin);
        w.Put(box.content != null);
    }

    protected override void Apply(Cardboard_Box box, NetDataReader r)
    {
        bool opened = r.GetBool();
        bool taped = r.GetBool();
        bool virgin = r.GetBool();
        bool hasContent = r.GetBool();

        if (box.taped && !taped)
            BreakTape(box);
        if (box.opened != opened)
            Toggle(box);
        box.virgin = virgin;
        if (!hasContent)
            box.content = null;
    }

    private static void Toggle(Cardboard_Box box)
    {
        // What's frozen in now; after an unfreeze, copies of it stay kinematic for their owners.
        Inside.Clear();
        if (box.frozenContents != null)
        {
            foreach (var frozen in box.frozenContents)
            {
                var o = frozen.obj != null ? NetObjects.Find(frozen.obj) : null;
                if (o != null)
                    Inside.Add(o);
            }
        }
        var delivery = box.content;
        box.content = null;
        box.Use();
        box.content = delivery;
        foreach (var o in Inside)
        {
            if (o.IsReplica && o.Body != null && !o.Body.isKinematic)
            {
                o.GameKinematic = false;
                o.Body.isKinematic = true;
            }
        }
        Inside.Clear();
    }
}
