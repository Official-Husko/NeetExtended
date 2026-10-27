using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Npcs;

/// <summary>What an NPC rig is doing: where it is, how it's turned (a corpse lies on its side) and its animator state.</summary>
internal struct NpcPose
{
    public const byte LeftHand = 1;
    public const byte RightHand = 2;
    public const byte Active = 4;

    public Vector3 Position;
    public Quaternion Rotation;
    public ushort Bools;
    public byte Dancing;
    public byte Speed;
    public byte Flags;

    public void Write(NetDataWriter writer)
    {
        writer.Put(Position.x);
        writer.Put(Position.y);
        writer.Put(Position.z);
        writer.Put(Rotation.x);
        writer.Put(Rotation.y);
        writer.Put(Rotation.z);
        writer.Put(Rotation.w);
        writer.Put(Bools);
        writer.Put(Dancing);
        writer.Put(Speed);
        writer.Put(Flags);
    }

    public static NpcPose Read(NetDataReader reader) => new NpcPose
    {
        Position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat()),
        Rotation = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat()),
        Bools = reader.GetUShort(),
        Dancing = reader.GetByte(),
        Speed = reader.GetByte(),
        Flags = reader.GetByte(),
    };

    public const int Size = 33;

    public bool SameAs(in NpcPose other) =>
        (Position - other.Position).sqrMagnitude < 1e-6f
        && Mathf.Abs(Quaternion.Dot(Rotation, other.Rotation)) > 0.99999f
        && Bools == other.Bools && Dancing == other.Dancing && Speed == other.Speed && Flags == other.Flags;
}
