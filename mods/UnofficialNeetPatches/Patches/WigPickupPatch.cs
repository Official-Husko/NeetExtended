using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// Wig.PickupTrigger turns collisions with the wearer's head back on with
/// Physics.IgnoreCollision(GetComponent&lt;BoxCollider&gt;(), ...) (Wig.cs:53). The thief's beanie has a
/// SphereCollider, so that gets null and the call throws, after Player_Abilities.Pickup already put
/// the beanie in the hand (Player_Abilities.cs:1144) but before it set who holds it (:1187). The hand
/// is left with an item that has no holder, and FixedUpdate then fails on it every step (:890).
/// The collider lookup asks for any Collider (what Usable.Start does for the same list, Usable.cs:304)
/// and the call skips missing colliders.
/// </summary>
[HarmonyPatch(typeof(Wig), nameof(Wig.PickupTrigger))]
[HarmonyPatchCategory(WigPickupFix.FeatureId)]
internal static class WigPickupPatch
{
    private static readonly MethodInfo IgnoreCollision =
        AccessTools.Method(typeof(Physics), nameof(Physics.IgnoreCollision), new[] { typeof(Collider), typeof(Collider), typeof(bool) });

    private static readonly MethodInfo SafeIgnore = AccessTools.Method(typeof(WigPickupPatch), nameof(IgnoreIfBoth));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        foreach (var instruction in code)
        {
            if (instruction.operand is MethodInfo method)
            {
                if (method.IsGenericMethod && method.Name == nameof(Component.GetComponent) &&
                    method.GetGenericArguments()[0] == typeof(BoxCollider))
                    instruction.operand = method.GetGenericMethodDefinition().MakeGenericMethod(typeof(Collider));
                else if (method == IgnoreCollision)
                    instruction.operand = SafeIgnore;
            }
            yield return instruction;
        }
    }

    private static void IgnoreIfBoth(Collider a, Collider b, bool ignore)
    {
        if (a != null && b != null)
            Physics.IgnoreCollision(a, b, ignore);
    }
}
