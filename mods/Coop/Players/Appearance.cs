using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// How a player looks: the clothes they wear (by item name: shoes, pants, shirt), which outfit the
/// body mesh shows, and the body shape. Sent when it changes.
/// </summary>
internal struct Appearance
{
    public const int Pieces = 3;

    // Blend shapes on the body mesh, as PlayerStats uses them.
    public const int FatShape = 2;
    public const int BodyTypeShape = 3;
    public const int MuscleShape = 4;

    public string Shoes, Pants, Shirt;
    public byte Attire;
    public float Fat, BodyType, Muscle;

    public string Piece(int index) => index == 0 ? Shoes : index == 1 ? Pants : Shirt;

    public static Appearance Of(Player_Abilities abilities)
    {
        var look = new Appearance
        {
            Shoes = ClothName(abilities, 0),
            Pants = ClothName(abilities, 1),
            Shirt = ClothName(abilities, 2),
            Attire = (byte)abilities.currentAttire,
        };
        var body = abilities.neetRend;
        if (body != null && body.sharedMesh != null)
        {
            look.Fat = Shape(body, FatShape);
            look.BodyType = Shape(body, BodyTypeShape);
            look.Muscle = Shape(body, MuscleShape);
        }
        return look;
    }

    public bool SameAs(in Appearance other) =>
        Shoes == other.Shoes && Pants == other.Pants && Shirt == other.Shirt && Attire == other.Attire &&
        Mathf.Approximately(Fat, other.Fat) && Mathf.Approximately(BodyType, other.BodyType) && Mathf.Approximately(Muscle, other.Muscle);

    public void Write(NetDataWriter writer, byte slot)
    {
        writer.Put(slot);
        writer.Put(Shoes ?? "");
        writer.Put(Pants ?? "");
        writer.Put(Shirt ?? "");
        writer.Put(Attire);
        writer.Put(Fat);
        writer.Put(BodyType);
        writer.Put(Muscle);
    }

    public static Appearance Read(NetDataReader reader, out byte slot)
    {
        slot = reader.GetByte();
        return new Appearance
        {
            Shoes = reader.GetString(),
            Pants = reader.GetString(),
            Shirt = reader.GetString(),
            Attire = reader.GetByte(),
            Fat = reader.GetFloat(),
            BodyType = reader.GetFloat(),
            Muscle = reader.GetFloat(),
        };
    }

    private static string ClothName(Player_Abilities abilities, int piece)
    {
        var clothes = abilities.clothings;
        if (clothes == null || piece >= clothes.Length || clothes[piece] == null)
            return "";
        return clothes[piece].TryGetComponent(out Usable usable) ? usable.prefabName ?? "" : "";
    }

    private static float Shape(SkinnedMeshRenderer body, int index) =>
        body.sharedMesh.blendShapeCount > index ? body.GetBlendShapeWeight(index) : 0f;
}
