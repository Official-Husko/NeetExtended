using Coop.World.Objects;
using HarmonyLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>
/// A fuse's charge, in whole units (devices drain it a little every frame; whole units keep the
/// traffic down). The setter recolours the fuse and re-checks the power it feeds.
/// </summary>
internal sealed class FuseBatteryState : StateType<Fuse>
{
    public override bool OnItems => true;

    protected override void Write(Fuse fuse, NetDataWriter w) => w.Put((short)Mathf.RoundToInt(fuse.BatteryPower));

    protected override void Apply(Fuse fuse, NetDataReader r)
    {
        float charge = r.GetShort();
        if (Mathf.Abs(fuse.BatteryPower - charge) >= 1f)
            fuse.BatteryPower = charge;
    }
}

/// <summary>
/// Which fuse sits in a smoke detector as its battery. Taking one out uses the detector's own
/// removal (without a hand to put it in); putting one in does what its collision handler does, minus
/// the fire check (alarms follow fire, which isn't shared yet).
/// </summary>
internal sealed class SmokeDetectorState : StateType<SmokeDetector>
{
    private static readonly AccessTools.FieldRef<SmokeDetector, SpriteRenderer> Indicator =
        AccessTools.FieldRefAccess<SmokeDetector, SpriteRenderer>("indicatorSprite");

    protected override void Write(SmokeDetector detector, NetDataWriter w)
    {
        var fuse = detector.Fuse;
        var o = fuse != null ? NetObjects.Find(fuse.gameObject) : null;
        (o != null ? o.Ref : default).Write(w);
    }

    protected override void Apply(SmokeDetector detector, NetDataReader r)
    {
        var wanted = NetRef.Read(r);
        var target = wanted.IsValid ? NetObjects.Find(wanted) : null;
        if (wanted.IsValid && target == null)
            return;
        var fuse = target != null ? target.Go.GetComponent<Fuse>() : null;
        if (detector.Fuse == fuse)
            return;
        if (detector.Fuse != null)
        {
            var old = NetObjects.Find(detector.Fuse.gameObject);
            detector.RemoveBattery(null);
            KeepReplica(old);
        }
        if (fuse != null)
            Insert(detector, fuse);
    }

    private static void Insert(SmokeDetector detector, Fuse fuse)
    {
        detector.Fuse = fuse;
        fuse.gameObject.SetActive(false);
        fuse.transform.parent = detector.transform;
        fuse.transform.position = Vector3.up * 100f;
        var indicator = Indicator(detector);
        if (indicator != null)
            indicator.color = fuse.currentColorIndicator;
    }

    private static void KeepReplica(NetObject o)
    {
        if (o == null || !o.IsReplica || o.Body == null)
            return;
        o.GameKinematic = o.Body.isKinematic;
        o.Body.isKinematic = true;
    }
}
