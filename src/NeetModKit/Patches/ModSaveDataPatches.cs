using HarmonyLib;
using NeetModKit.Saves;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Patches;

[HarmonyPatch(typeof(SaveLoadHandler))]
internal static class ModSaveDataPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(SaveLoadHandler.AllSaveDataToJSon))]
    private static void AddModEntry(JObject __result) => ModSaveData.Write(__result);

    [HarmonyPrefix]
    [HarmonyPatch(nameof(SaveLoadHandler.LoadAll))]
    private static void ReadModEntry() => ModSaveData.BeforeLoad();

    [HarmonyPostfix]
    [HarmonyPatch(nameof(SaveLoadHandler.LoadAll))]
    private static void DeliverModEntry() => ModSaveData.AfterLoad();
}
