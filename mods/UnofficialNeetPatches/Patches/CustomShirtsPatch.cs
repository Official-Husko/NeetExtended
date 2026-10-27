using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// The custom shop puts its picture on an item through a MaterialPropertyBlock on the item's
/// renderer (CustomItemShop.CustomizeItem, CustomItemShop.cs:114-128). Putting clothes on copies the
/// renderer's material into the body (Player_Abilities.PutOnClothes, :1428-1460), which has no
/// property block, so a shirt bought this session is plain on the body. Loading a save sets the
/// picture on the material itself (CustomTextureSaveLoader.Load), which is why it shows after a
/// reload. A clothing item customized now gets the picture on its material too.
/// </summary>
[HarmonyPatch(typeof(CustomItemShop), "CustomizeItem", new[] { typeof(GameObject) })]
[HarmonyPatchCategory(CustomShirtsFix.FeatureId)]
internal static class CustomShirtsPatch
{
    private static void Postfix(CustomItemShop __instance, GameObject obj)
    {
        if (obj == null || __instance.previewImg == null || !obj.TryGetComponent(out Wearable _))
            return;
        var renderer = obj.GetComponentInChildren<Renderer>();
        if (renderer != null)
            renderer.material.SetTexture("_MainTex", __instance.previewImg.mainTexture);
    }
}
