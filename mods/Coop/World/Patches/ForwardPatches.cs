using Coop.World.Forwarding;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>An explosion here pushes other players' real bodies too; see <see cref="Forwarder"/>.</summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch(typeof(Commons), nameof(Commons.ExplosionDamage))]
internal static class ForwardPatches
{
    private static void Postfix(Vector3 pos, float radius) => Forwarder.Exploded(pos, radius);
}
