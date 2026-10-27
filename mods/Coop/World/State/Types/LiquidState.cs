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
/// Layout: filled (bool), liquid (byte, the paint channel), drink left in tenths (ushort). All zero
/// is an empty container.
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
    }

    protected override void Apply(LiquidContainer glass, NetDataReader r)
    {
        bool filled = r.GetBool();
        int liquid = r.GetByte();
        float left = r.GetUShort() / 10f;
        if (glass.waterVolume == null)
            return;

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
