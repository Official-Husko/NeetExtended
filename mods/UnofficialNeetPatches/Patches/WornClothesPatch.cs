using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// Worn clothes are parked at y = -500 with a kinematic body (Wearable.Wear, Wearable.cs:20-33). A
/// save stores the body's kinematic flag (Usable.cs:241) but the loader never applies it (nothing
/// in SaveLoadHandler.LoadUsables sets isKinematic), and Player_Abilities.Load puts the loaded
/// clothes on with PutOnClothes directly (Player_Abilities.cs:361-369), not through Wear. So
/// after every load the worn clothes are ordinary falling items at the bottom of the world.
/// PutOnClothes now makes a worn piece kinematic, and brings it back to the parking spot if it is
/// already falling.
/// </summary>
[HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.PutOnClothes))]
[HarmonyPatchCategory(WornClothesFix.FeatureId)]
internal static class WornClothesPatch
{
    private static readonly Vector3 Parking = new Vector3(0f, -500f, 0f);

    private static void Postfix(Player_Abilities __instance, GameObject clothObj)
    {
        // Rigs without clothes don't wear anything, so there's nothing to park.
        if (clothObj == null || __instance.noClothUVs)
            return;
        var body = clothObj.GetComponent<Rigidbody>();
        if (body == null || body.isKinematic)
            return;
        body.isKinematic = true;
        clothObj.transform.position = Parking;
    }
}
