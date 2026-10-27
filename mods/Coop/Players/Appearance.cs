using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// How a player looks: the clothes they wear (shoes, pants, shirt), which outfit the body mesh shows,
/// and the body shape. Sent when it changes.
///
/// Each worn piece travels as the shared item itself (its net ref, so the other games dress the body
/// in that item's own material: a custom picture, or whatever else the item shows), its prefab name
/// (the stand-in while the item isn't shared or hasn't arrived yet) and a dirty bit (the pants after
/// an accident). Layout: slot, shoes, pants, shirt names, attire, fat, body type, muscle, then the
/// three item refs and the dirty bits.
/// </summary>
internal struct Appearance
{
    public const int Pieces = 3;

    // Blend shapes on the body mesh, as PlayerStats uses them.
    public const int FatShape = 2;
    public const int BodyTypeShape = 3;
    public const int MuscleShape = 4;

    public string Shoes, Pants, Shirt;
    public NetRef ShoesItem, PantsItem, ShirtItem;

    /// <summary>Bit 0 shoes, 1 pants, 2 shirt: the piece is dirty (<c>Wearable.dirty</c>).</summary>
    public byte Dirty;
    public byte Attire;
    public float Fat, BodyType, Muscle;

    public string Piece(int index) => index == 0 ? Shoes : index == 1 ? Pants : Shirt;

    public NetRef Item(int index) => index == 0 ? ShoesItem : index == 1 ? PantsItem : ShirtItem;

    public bool IsDirty(int index) => (Dirty & (1 << index)) != 0;

    public static Appearance Of(Player_Abilities abilities)
    {
        var look = new Appearance { Attire = (byte)abilities.currentAttire };
        look.Shoes = Worn(abilities, 0, ref look.ShoesItem, ref look.Dirty);
        look.Pants = Worn(abilities, 1, ref look.PantsItem, ref look.Dirty);
        look.Shirt = Worn(abilities, 2, ref look.ShirtItem, ref look.Dirty);
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
        ShoesItem.Equals(other.ShoesItem) && PantsItem.Equals(other.PantsItem) && ShirtItem.Equals(other.ShirtItem) && Dirty == other.Dirty &&
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
        ShoesItem.Write(writer);
        PantsItem.Write(writer);
        ShirtItem.Write(writer);
        writer.Put(Dirty);
    }

    public static Appearance Read(NetDataReader reader, out byte slot)
    {
        slot = reader.GetByte();
        var look = new Appearance
        {
            Shoes = reader.GetString(),
            Pants = reader.GetString(),
            Shirt = reader.GetString(),
            Attire = reader.GetByte(),
            Fat = reader.GetFloat(),
            BodyType = reader.GetFloat(),
            Muscle = reader.GetFloat(),
        };
        look.ShoesItem = NetRef.Read(reader);
        look.PantsItem = NetRef.Read(reader);
        look.ShirtItem = NetRef.Read(reader);
        look.Dirty = reader.GetByte();
        return look;
    }

    // The prefab name of what is worn on a piece, its shared item (none while the game hasn't shared
    // it yet) and whether it is dirty.
    private static string Worn(Player_Abilities abilities, int piece, ref NetRef item, ref byte dirty)
    {
        var clothes = abilities.clothings;
        if (clothes == null || piece >= clothes.Length || clothes[piece] == null)
            return "";
        var worn = clothes[piece];
        var shared = NetObjects.Find(worn);
        if (shared != null)
            item = shared.Ref;
        if (worn.TryGetComponent(out Wearable wearable) && wearable.dirty)
            dirty |= (byte)(1 << piece);
        return worn.TryGetComponent(out Usable usable) ? usable.prefabName ?? "" : "";
    }

    private static float Shape(SkinnedMeshRenderer body, int index) =>
        body.sharedMesh.blendShapeCount > index ? body.GetBlendShapeWeight(index) : 0f;
}
