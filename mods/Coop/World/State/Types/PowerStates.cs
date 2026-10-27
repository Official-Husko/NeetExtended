using Coop.World.Objects;
using LiteNetLib.Utils;

namespace Coop.World.State.Types;

/// <summary>A breaker in the fuse box. Its setter turns the lever and powers the room up or down.</summary>
internal sealed class FuseButtonState : StateType<FuseBox_Button>
{
    protected override void Write(FuseBox_Button button, NetDataWriter w) => w.Put(button.IsOn);

    protected override void Apply(FuseBox_Button button, NetDataReader r)
    {
        bool on = r.GetBool();
        if (button.IsOn != on)
            button.IsOn = on;
    }
}

/// <summary>
/// A fuse box slot: which fuse is in it (the fuse is a shared item). The slot's own insert and expel
/// place the fuse and power the room; a fuse that's a replica here stays kinematic for its owner.
/// </summary>
internal sealed class FuseSlotState : StateType<FuseBox_Slot>
{
    protected override void Write(FuseBox_Slot slot, NetDataWriter w)
    {
        var fuse = slot.insertedFuse;
        var o = fuse != null ? NetObjects.Find(fuse.gameObject) : null;
        (o != null ? o.Ref : default).Write(w);
    }

    protected override void Apply(FuseBox_Slot slot, NetDataReader r)
    {
        var wanted = NetRef.Read(r);
        var target = wanted.IsValid ? NetObjects.Find(wanted) : null;
        if (wanted.IsValid && target == null)
            return; // The fuse hasn't arrived here yet; the next change or manifest fixes it.
        var fuse = target != null ? target.Go.GetComponent<Fuse>() : null;
        if (slot.insertedFuse == fuse)
            return;
        if (slot.insertedFuse != null)
        {
            var old = NetObjects.Find(slot.insertedFuse.gameObject);
            slot.ExpellFuse();
            KeepReplica(old);
        }
        if (fuse != null)
        {
            slot.InsertFuse(fuse);
            KeepReplica(target);
        }
    }

    // The slot sets the fuse's body the way the game wants it; a replica stays kinematic and remembers that.
    private static void KeepReplica(NetObject o)
    {
        if (o == null || !o.IsReplica || o.Body == null)
            return;
        o.GameKinematic = o.Body.isKinematic;
        o.Body.isKinematic = true;
    }
}
