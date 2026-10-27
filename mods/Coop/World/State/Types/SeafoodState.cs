using System;
using System.Reflection;
using HarmonyLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>
/// A Seafood Delight tin: opened (the lid off) and popped. Popping throws the tin and its chunks
/// about where it's simulated, and those travel as bodies and spawns; here the tin only shows the lid
/// off and the steam. Opening it by hand stops it swelling, so the others stop it too.
/// </summary>
internal sealed class SeafoodState : StateType<Seafood_Delight>
{
    private static readonly MethodInfo Swell = AccessTools.Method(typeof(Seafood_Delight), "Inflate");

    public override bool OnItems => true;

    protected override void Write(Seafood_Delight tin, NetDataWriter w)
    {
        w.Put(tin.opened);
        w.Put(tin.exploded);
    }

    protected override void Apply(Seafood_Delight tin, NetDataReader r)
    {
        bool opened = r.GetBool();
        bool exploded = r.GetBool();
        if (opened && !tin.opened)
        {
            tin.opened = true;
            Open(tin);
        }
        if (exploded && !tin.exploded)
        {
            tin.exploded = true;
            tin.opened = true;
            Open(tin);
            var steam = tin.GetComponentInChildren<ParticleSystem>();
            if (steam != null)
                steam.Play();
        }
    }

    private static void Open(Seafood_Delight tin)
    {
        if (tin.lid != null)
            tin.lid.SetActive(false);
        var food = tin.GetComponent<Food>();
        if (food != null)
            food.onCookingChg = (Action)Delegate.Remove(food.onCookingChg, (Action)Delegate.CreateDelegate(typeof(Action), tin, Swell));
    }
}
