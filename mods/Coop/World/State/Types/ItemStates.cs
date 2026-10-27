using System;
using HarmonyLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>A flashlight's beam.</summary>
internal sealed class FlashlightState : StateType<Flashlight>
{
    private static readonly AccessTools.FieldRef<Flashlight, Light> Beam = AccessTools.FieldRefAccess<Flashlight, Light>("spotLight");

    public override bool OnItems => true;

    protected override void Write(Flashlight light, NetDataWriter w) => w.Put(BeamOf(light)?.enabled ?? false);

    protected override void Apply(Flashlight light, NetDataReader r)
    {
        bool on = r.GetBool();
        var beam = BeamOf(light);
        if (beam != null && beam.enabled != on)
            light.SetLight(on, noAudio: false);
    }

    // Filled in Start; a flashlight that hasn't started yet still has its light child.
    private static Light BeamOf(Flashlight light) => Beam(light) ?? light.GetComponentInChildren<Light>(true);
}

/// <summary>A BBQ lighter: lit or not (its flame lights and goes out with it).</summary>
internal sealed class LighterState : StateType<Lighter_BBQ>
{
    private static readonly Action<Lighter_BBQ, bool> Toggle =
        AccessTools.MethodDelegate<Action<Lighter_BBQ, bool>>(AccessTools.Method(typeof(Lighter_BBQ), "Toggle"));

    public override bool OnItems => true;

    protected override void Write(Lighter_BBQ lighter, NetDataWriter w) => w.Put(lighter.isOn);

    protected override void Apply(Lighter_BBQ lighter, NetDataReader r)
    {
        if (lighter.isOn != r.GetBool())
            Toggle(lighter, false);
    }
}
