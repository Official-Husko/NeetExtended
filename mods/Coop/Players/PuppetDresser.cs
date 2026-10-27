using System.Collections.Generic;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Dresses a remote player's body the way the game dresses the player: each worn piece puts the
/// clothing item's material on its slot of the body (the skin otherwise), the body mesh shows the
/// UV set for the outfit, and the shirt also colours the hands.
/// </summary>
internal static class PuppetDresser
{
    private const int SkinSlot = 3;
    private const int HandClothSlot = 1;

    private static readonly List<Vector2> Uvs = new List<Vector2>();

    public static void Apply(SkinnedMeshRenderer body, SkinnedMeshRenderer[] hands, in Appearance look)
    {
        if (body == null)
            return;
        var materials = body.sharedMaterials;
        if (materials.Length <= SkinSlot)
            return;
        var skin = materials[SkinSlot];
        for (int piece = 0; piece < Appearance.Pieces; piece++)
            materials[piece] = ClothMaterial(look.Piece(piece)) ?? skin;
        body.sharedMaterials = materials;

        foreach (var hand in hands)
        {
            if (hand == null)
                continue;
            var handMaterials = hand.sharedMaterials;
            if (handMaterials.Length > HandClothSlot)
            {
                handMaterials[HandClothSlot] = materials[2];
                hand.sharedMaterials = handMaterials;
            }
        }

        ShowOutfit(body, look.Attire);
        SetShape(body, Appearance.FatShape, look.Fat);
        SetShape(body, Appearance.BodyTypeShape, look.BodyType);
        SetShape(body, Appearance.MuscleShape, look.Muscle);
    }

    private static Material ClothMaterial(string item)
    {
        if (string.IsNullOrEmpty(item) || !PickupBank.pickupsDict.TryGetValue(item, out var spawnable) || spawnable.obj == null)
            return null;
        return spawnable.obj.TryGetComponent(out MeshRenderer renderer) ? renderer.sharedMaterial : null;
    }

    // The game keeps one UV set per outfit and copies the right one into the mesh's first set. Our
    // body has its own mesh, but its sets came from the shared one, whose first set the local player
    // keeps changing; the local player's untouched copy of the sets is the reliable source.
    private static void ShowOutfit(SkinnedMeshRenderer body, byte attire)
    {
        var player = HouseManager._ != null ? HouseManager._.player : null;
        var sets = player != null && player.TryGetComponent(out Player_Abilities abilities) ? abilities.tempUVSwaps : null;
        if (sets != null && attire < sets.Length && sets[attire] != null)
        {
            body.sharedMesh.SetUVs(0, sets[attire]);
            return;
        }
        if (attire < 8 && body.sharedMesh.uv.Length > 0)
        {
            body.sharedMesh.GetUVs(attire, Uvs);
            if (Uvs.Count > 0)
                body.sharedMesh.SetUVs(0, Uvs);
        }
    }

    private static void SetShape(SkinnedMeshRenderer body, int index, float weight)
    {
        if (body.sharedMesh.blendShapeCount > index)
            body.SetBlendShapeWeight(index, weight);
    }
}
