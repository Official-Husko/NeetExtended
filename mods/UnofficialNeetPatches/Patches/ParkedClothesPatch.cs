using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// The other half of <see cref="WornClothesPatch"/>: clothes that were parked when the game saved but
/// that nobody puts on again while loading. Those are the clothes of characters who died or left
/// (their bodies are gone, the parked clothes stay in the save) and, in co-op, other players' clothes
/// the host keeps for their return. The load gives them back a falling body like any other item
/// (the saved kinematic flag is never applied), so they fell forever below the house, and the next
/// save stored that. After a load, any piece of clothing still below the parking height goes back to
/// being parked: kinematic, hidden, at the parking spot.
/// </summary>
[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.LoadAll))]
[HarmonyPatchCategory(WornClothesFix.FeatureId)]
internal static class ParkedClothesPatch
{
    private static readonly Vector3 Parking = new Vector3(0f, -500f, 0f);

    // Nothing loose in the house is anywhere near this low; parked or falling pieces are.
    private const float ParkedBelow = -400f;

    private static void Postfix()
    {
        foreach (var piece in Object.FindObjectsOfType<Wearable>())
        {
            if (piece.transform.position.y > ParkedBelow || !piece.TryGetComponent(out Rigidbody body) || body.isKinematic)
                continue;
            body.isKinematic = true;
            body.velocity = Vector3.zero;
            piece.transform.position = Parking;
            if (piece.TryGetComponent(out MeshRenderer mesh))
                mesh.enabled = false;
        }
    }
}
