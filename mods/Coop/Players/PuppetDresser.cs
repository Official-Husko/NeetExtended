using System.Collections.Generic;
using Coop.World.Objects;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Dresses a remote player's body the way the game dresses the player: each worn piece puts the
/// clothing item's material on its slot of the body (the skin otherwise), the body mesh shows the
/// UV set for the outfit, and the shirt also colours the hands.
///
/// The material is the shared item's own (its renderer's material, the one <c>PutOnClothes</c> takes),
/// so a custom picture on a shirt shows, and shows once its file has arrived, because the file loads
/// onto that very material. Dirty pieces get the game's dirty material. A piece whose item isn't
/// here yet shows the plain prefab for now and <see cref="Apply"/> says so, so it can be done again.
/// </summary>
internal static class PuppetDresser
{
    private const int SkinSlot = 3;
    private const int HandClothSlot = 1;

    private static readonly List<Vector2> Uvs = new List<Vector2>();
    private static Material _dirtyMaterial;

    /// <summary>Dresses the body; false when a worn item isn't in this game yet (try again later).</summary>
    public static bool Apply(SkinnedMeshRenderer body, SkinnedMeshRenderer[] hands, in Appearance look)
    {
        if (body == null)
            return true;
        var materials = body.sharedMaterials;
        if (materials.Length <= SkinSlot)
            return true;
        var skin = materials[SkinSlot];
        bool complete = true;
        for (int piece = 0; piece < Appearance.Pieces; piece++)
            materials[piece] = ClothMaterial(look, piece, ref complete) ?? skin;
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
        return complete;
    }

    private static Material ClothMaterial(in Appearance look, int piece, ref bool complete)
    {
        string name = look.Piece(piece);
        if (string.IsNullOrEmpty(name) && !look.Item(piece).IsValid)
            return null;
        if (look.IsDirty(piece))
        {
            if (_dirtyMaterial == null)
                _dirtyMaterial = Resources.Load<Material>("Materials/Shitty");
            if (_dirtyMaterial != null)
                return _dirtyMaterial;
        }
        var reference = look.Item(piece);
        if (reference.IsValid)
        {
            var item = NetObjects.Find(reference);
            // .material, like PutOnClothes: it makes the item's own copy, and later changes to the
            // item (its picture arriving) are made to that copy.
            if (item != null && item.Alive && item.Go.TryGetComponent(out MeshRenderer worn))
                return worn.material;
            complete = false;
        }
        if (string.IsNullOrEmpty(name) || !PickupBank.pickupsDict.TryGetValue(name, out var spawnable) || spawnable.obj == null)
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
