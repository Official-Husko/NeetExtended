using System.Collections.Generic;
using HarmonyLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>
/// A laser pointer on or off. Switching it shows its beam and its dot and lights the dot's flame (the
/// dot burns, which is how the pointer lights things and burns whoever it points at); every game
/// does the same with the game's own setter and then runs the pointer's Update itself, so the dot
/// follows the pointer. A pointer that hasn't started yet is switched when it does: its Start
/// switches it off, which would otherwise undo a state that arrived first.
///
/// Layout: on (bool).
/// </summary>
internal sealed class LaserState : StateType<LaserPointer>
{
    private static readonly AccessTools.FieldRef<LaserPointer, LineRenderer> Beam =
        AccessTools.FieldRefAccess<LaserPointer, LineRenderer>("lineRend");

    private static readonly HashSet<int> WaitingToStart = new HashSet<int>();

    public override bool OnItems => true;

    // The use of the pointer marks it (HeatPatches), so there is no need to look at them all.
    public override bool Swept => false;

    protected override void Write(LaserPointer laser, NetDataWriter w) => w.Put(laser.Toggled);

    protected override void Apply(LaserPointer laser, NetDataReader r)
    {
        bool on = r.GetBool();
        if (Beam(laser) == null)
        {
            // Not started: the setter needs the line renderer, and Start switches the pointer off.
            if (on)
                WaitingToStart.Add(laser.GetInstanceID());
            else
                WaitingToStart.Remove(laser.GetInstanceID());
            return;
        }
        if (laser.Toggled != on)
            laser.Toggled = on;
    }

    /// <summary>After the pointer's Start: switches it on if the state said so before.</summary>
    public static void Started(LaserPointer laser)
    {
        if (!WaitingToStart.Remove(laser.GetInstanceID()))
            return;
        Restoring = true;
        try
        {
            laser.Toggled = true;
        }
        finally
        {
            Restoring = false;
        }
    }

    /// <summary>True while a pointer is switched on after its Start: that isn't a change to send.</summary>
    public static bool Restoring { get; private set; }

    public static void Clear() => WaitingToStart.Clear();
}
