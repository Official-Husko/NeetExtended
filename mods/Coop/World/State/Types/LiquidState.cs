using HarmonyLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>
/// What a glass, bottle or other liquid container holds: empty or full, which liquid, and how much of
/// it is left to drink. Filling, tipping and sipping happen where the container is simulated; the
/// others fill and empty theirs the same way, without the splash object (that one is made, and
/// shared, where it was tipped).
///
/// It also carries how long the container has stood in a fridge (seconds, up to 30) and how many
/// fridges it is in: a glass chilled for 20 seconds tips out as ice, and the game that picks it up
/// next has to know that. A game that only shows the glass doesn't see it enter the fridge.
///
/// Layout: filled (bool), liquid (byte, the paint channel), drink left in tenths (ushort), chill
/// (byte), fridges (byte). All zero is an empty container out of the fridge.
/// </summary>
internal sealed class LiquidState : StateType<LiquidContainer>
{
    private static readonly AccessTools.FieldRef<LiquidContainer, ParticleSystem> Splash =
        AccessTools.FieldRefAccess<LiquidContainer, ParticleSystem>("splashParticles");

    public override bool OnItems => true;

    protected override void Write(LiquidContainer glass, NetDataWriter w)
    {
        bool filled = glass.filled;
        w.Put(filled);
        w.Put((byte)(filled && glass.waterVolume != null ? glass.waterVolume.paintChannel : 0));
        w.Put((ushort)(filled ? Mathf.Clamp(Mathf.RoundToInt(glass.currentDrinkVolume * 10f), 0, ushort.MaxValue) : 0));
        w.Put((byte)Mathf.Clamp(Mathf.RoundToInt(glass.frozenContentsTimer), 0, 30));
        w.Put((byte)Mathf.Clamp(glass.nearbyRefridgeratorCount, 0, 255));
    }

    protected override void Apply(LiquidContainer glass, NetDataReader r)
    {
        bool filled = r.GetBool();
        int liquid = r.GetByte();
        float left = r.GetUShort() / 10f;
        float chill = r.GetByte();
        int fridges = r.GetByte();
        if (glass.waterVolume == null)
            return;
        if (Mathf.Abs(glass.frozenContentsTimer - chill) >= 1f)
            glass.frozenContentsTimer = chill;
        glass.nearbyRefridgeratorCount = fridges;

        if (!filled)
        {
            if (!glass.filled)
                return;
            glass.Empty(null, noSplash: true);
            var splash = Splash(glass);
            if (splash != null)
                splash.Play();
            return;
        }
        if (!glass.filled || glass.waterVolume.paintChannel != liquid)
            glass.Fill(liquid);
        glass.currentDrinkVolume = left;
    }
}
