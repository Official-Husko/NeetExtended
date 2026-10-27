using HarmonyLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>
/// How hot a pan (or anything else with a HeatHolder) is. Only the game that simulates the pan works
/// the heat out (flames in contact warm it, time and water cool it, see HeatPatches); the others take
/// the number from here, so the pan's flame level, which is what cooks the food in it, burns whoever
/// touches it and makes it glow, is the same in every game. The heat goes out in whole units, rounded
/// down: the game's flame levels switch at whole units (20, 40, 75), so the level is never ahead of
/// the owner's, and the heat moves at most one unit a second, so this is at most about two small
/// messages a second while a pan warms up or cools down.
///
/// Layout: heat (byte, 0 to 100).
/// </summary>
internal sealed class HeatState : StateType<HeatHolder>
{
    private static readonly AccessTools.FieldRef<HeatHolder, Material> Glow =
        AccessTools.FieldRefAccess<HeatHolder, Material>("emitMat");

    public override bool OnItems => true;

    protected override void Write(HeatHolder holder, NetDataWriter w) =>
        w.Put((byte)Mathf.Clamp(Mathf.FloorToInt(holder._heatIndex), 0, 100));

    protected override void Apply(HeatHolder holder, NetDataReader r)
    {
        float heat = r.GetByte();
        // A pan that hasn't started yet has no glow material for its setter; it is told again when it changes.
        if (holder.emissiveHeat && Glow(holder) == null)
            return;
        if (Mathf.FloorToInt(holder._heatIndex) != (int)heat)
            holder.HeatIndex = heat;
    }
}
