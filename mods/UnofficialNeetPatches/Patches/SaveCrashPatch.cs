using System;
using System.Collections.Generic;
using HarmonyLib;
using NeetModKit.Patching;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// AllSaveDataToJSon calls every save subscriber through Delegate.DynamicInvoke with nothing
/// around it, so one subscriber that throws ends the whole save (and every later one, since the
/// broken object stays). The transpiler routes each call through <see cref="Run"/>, which leaves a
/// failing subscriber out and reports it. It also calls the delegate directly instead of through
/// reflection.
/// </summary>
[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.AllSaveDataToJSon))]
[HarmonyPatchCategory(SaveCrashesFix.FeatureId)]
internal static class SaveCrashPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) =>
        CallSwap.Replace(code,
            AccessTools.Method(typeof(Delegate), nameof(Delegate.DynamicInvoke)),
            AccessTools.Method(typeof(SaveCrashPatch), nameof(Run)));

    private static object Run(Delegate subscriber, object[] args)
    {
        try
        {
            return subscriber is Func<SaveData> save ? save() : subscriber.DynamicInvoke(args);
        }
        catch (Exception e)
        {
            PatchReports.SaveSkipped(subscriber.Target, e);
            return null;
        }
    }
}
