using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// The save lists every mask, hat, pair of glasses and cigarette the body wears (clothingIds from
/// index 3 on, Player_Abilities.GetClothingIds), but Player_Abilities.Load only puts on the one at
/// index 3 (Player_Abilities.cs:371-374). The others are left as plain loaded items at the place they
/// had on the head, hanging there (the saved kinematic flag is never applied, so they drop). This
/// puts the rest on with the game's own PutOnMask, which refuses what can't be worn together
/// (two non-stackable masks) the same way a click would.
/// </summary>
[HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.Load))]
[HarmonyPatchCategory(MasksAfterLoadFix.FeatureId)]
internal static class MasksAfterLoadPatch
{
    // clothingIds: shoes, pants, shirt, then the masks; the game itself restores the first.
    private const int FirstMask = 3;

    private static void Postfix(Player_Abilities __instance, JToken jt)
    {
        if (!(jt?["clothingIds"] is JArray ids) || SaveLoadHandler.loadedUsablesById == null)
            return;
        for (int i = FirstMask + 1; i < ids.Count; i++)
        {
            int id = ids[i].Value<int>();
            if (id == -1 || !SaveLoadHandler.loadedUsablesById.TryGetValue(id, out var item) || item == null)
                continue;
            if (item.TryGetComponent(out MaskFace mask) && !__instance.masks.Contains(item))
                mask.PutOnMask(__instance.gameObject, notHeld: true);
        }
    }
}
