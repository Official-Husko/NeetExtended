using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NeetModKit.Patching;
using PerformanceFixes.Features;
using PerformanceFixes.Logging;
using UnityEngine;

namespace PerformanceFixes.Patches;

/// <summary>The game methods that set velocities every physics step, routed through <see cref="QuietPhysics"/>.</summary>
[HarmonyPatch]
[HarmonyPatchCategory(LogSpamFeature.FeatureId)]
internal static class KinematicVelocityPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Player_Movement), "FixedUpdate");
        yield return AccessTools.Method(typeof(NPCNavigationTest), "FixedUpdate");
        yield return AccessTools.Method(typeof(BrainPlayerHijack), "FixedUpdate");
        yield return AccessTools.Method(typeof(Door), "FixedUpdate");
        yield return AccessTools.Method(typeof(Player_Abilities), nameof(Player_Abilities.RaisePhysicsHand));
        yield return AccessTools.Method(typeof(Player_Abilities), nameof(Player_Abilities.CenterPickup));
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        code = CallSwap.Replace(code,
            AccessTools.PropertySetter(typeof(Rigidbody), nameof(Rigidbody.velocity)),
            AccessTools.Method(typeof(QuietPhysics), nameof(QuietPhysics.SetVelocity)));
        return CallSwap.Replace(code,
            AccessTools.PropertySetter(typeof(Rigidbody), nameof(Rigidbody.angularVelocity)),
            AccessTools.Method(typeof(QuietPhysics), nameof(QuietPhysics.SetAngularVelocity)));
    }
}

/// <summary>Player_Movement.Update prints "restinput" every time a rig starts or stops crouching.</summary>
[HarmonyPatch(typeof(Player_Movement), "Update")]
[HarmonyPatchCategory(LogSpamFeature.FeatureId)]
internal static class CrouchPrintPatch
{
    private static readonly MethodInfo Print = AccessTools.Method(typeof(MonoBehaviour), nameof(MonoBehaviour.print));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var list = new List<CodeInstruction>(code);
        for (int i = 0; i + 1 < list.Count; i++)
        {
            if (list[i].opcode == OpCodes.Ldstr && list[i].operand as string == "restinput" && list[i + 1].Calls(Print))
            {
                list[i].opcode = OpCodes.Nop;
                list[i].operand = null;
                list[i + 1].opcode = OpCodes.Nop;
                list[i + 1].operand = null;
            }
        }
        return list;
    }
}
