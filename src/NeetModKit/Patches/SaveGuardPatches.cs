using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Patches;

/// <summary>
/// Keeps a save loadable when content it mentions is gone (usually because a mod was removed).
/// Without this, one unknown item or NPC name throws inside LoadAll and nothing after it loads.
/// </summary>
[HarmonyPatch]
internal static class SaveGuardPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SaveLoadHandler), "GetTokensByType")]
    private static void DropUnknownContent(string type, List<JToken> __result)
    {
        switch (type)
        {
            case "Usable":
                Drop(__result, t => t.Value<string>("prefabName"), PickupBank.pickupsDict.ContainsKey, "item");
                break;
            case "Brain":
                Drop(__result, t => t["basicBrainData"]?.Value<string>("prefabName"), BrainBank.brainsDict.ContainsKey, "NPC");
                break;
        }
    }

    /// <summary>The card game loads saved cards by index; trim extras so a smaller card list doesn't throw.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AnimeCardGenerator), nameof(AnimeCardGenerator.Load), typeof(JToken), typeof(bool))]
    private static void TrimSavedCards(AnimeCardGenerator __instance, JToken jt, bool resetOnly)
    {
        if (resetOnly || !(jt?["subSaveData"] is JArray saved))
            return;

        int cards = __instance.GetComponentsInChildren<AnimeCard>(true).Length;
        if (saved.Count <= cards)
            return;

        KitMod.Instance.Log.Warn($"Save has {saved.Count} anime cards but only {cards} exist; ignoring the extra {saved.Count - cards}.");
        while (saved.Count > cards)
            saved.RemoveAt(saved.Count - 1);
    }

    private static void Drop(List<JToken> tokens, System.Func<JToken, string> nameOf, System.Func<string, bool> known, string kind)
    {
        var missing = tokens.Select(nameOf).Where(n => !string.IsNullOrEmpty(n) && !known(n)).ToList();
        if (missing.Count == 0)
            return;

        tokens.RemoveAll(t =>
        {
            string n = nameOf(t);
            return !string.IsNullOrEmpty(n) && !known(n);
        });

        var summary = missing.GroupBy(n => n).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key);
        KitMod.Instance.Log.Warn($"Skipped saved {kind}(s) that no longer exist: {string.Join(", ", summary)}");
    }
}
