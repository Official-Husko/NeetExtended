using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// One mask, hat, pair of glasses or cigarette a player wears: the shared item and where the wearer's
/// game put it relative to its camera (<c>MaskFace.PutOnMask</c> parents it to the camera, with the
/// mask's offset, and a random one for stackable things). Part of <see cref="Appearance"/>.
/// Layout: item ref, local position (3 floats), local rotation (4 floats).
/// </summary>
internal struct MaskLook
{
    public const int MostWorn = 8;

    public NetRef Item;
    public Vector3 Position;
    public Quaternion Rotation;

    public void Write(NetDataWriter writer)
    {
        Item.Write(writer);
        writer.Put(Position.x);
        writer.Put(Position.y);
        writer.Put(Position.z);
        writer.Put(Rotation.x);
        writer.Put(Rotation.y);
        writer.Put(Rotation.z);
        writer.Put(Rotation.w);
    }

    public static MaskLook Read(NetDataReader reader) => new MaskLook
    {
        Item = NetRef.Read(reader),
        Position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat()),
        Rotation = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat()),
    };

    /// <summary>The shared things this player wears on their face (none while the game hasn't shared them yet).</summary>
    public static MaskLook[] Of(Player_Abilities abilities)
    {
        var worn = abilities.masks;
        if (worn == null || worn.Count == 0)
            return null;
        MaskLook[] looks = null;
        int count = 0;
        for (int i = 0; i < worn.Count && count < MostWorn; i++)
        {
            var item = worn[i] != null ? NetObjects.Find(worn[i]) : null;
            if (item == null)
                continue;
            looks ??= new MaskLook[Mathf.Min(worn.Count, MostWorn)];
            looks[count++] = new MaskLook { Item = item.Ref, Position = item.Tf.localPosition, Rotation = item.Tf.localRotation };
        }
        if (looks != null && count < looks.Length)
            System.Array.Resize(ref looks, count);
        return looks;
    }

    /// <summary>Same items worn (where they sit doesn't change while they're on).</summary>
    public static bool Same(MaskLook[] a, MaskLook[] b)
    {
        int length = a?.Length ?? 0;
        if (length != (b?.Length ?? 0))
            return false;
        for (int i = 0; i < length; i++)
        {
            if (!a[i].Item.Equals(b[i].Item))
                return false;
        }
        return true;
    }
}
