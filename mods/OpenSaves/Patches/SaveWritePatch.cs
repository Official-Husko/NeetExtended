using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using NeetModKit.Patching;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSaves.Writing;

namespace OpenSaves.Patches;

/// <summary>While Compressed saves is on, SaveAll writes minified JSON into a .nsav file.</summary>
[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.SaveAll))]
[HarmonyPatchCategory(Features.CompressedSavesFeature.FeatureId)]
internal static class SaveWritePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        code = CallSwap.Replace(code,
            AccessTools.Method(typeof(File), nameof(File.WriteAllText), new[] { typeof(string), typeof(string) }),
            AccessTools.Method(typeof(SaveWriter), nameof(SaveWriter.WriteSave)));
        return CallSwap.Replace(code,
            AccessTools.Method(typeof(JToken), nameof(JToken.ToString), new[] { typeof(Formatting), typeof(JsonConverter[]) }),
            AccessTools.Method(typeof(SaveWriter), nameof(SaveWriter.Serialize)));
    }
}
