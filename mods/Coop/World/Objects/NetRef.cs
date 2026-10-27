using System;
using LiteNetLib.Utils;

namespace Coop.World.Objects;

public enum NetKind : byte
{
    None = 0,

    /// <summary>A saved item: Usable.saveId.</summary>
    Item = 1,

    /// <summary>An NPC, rat, cat and so on: Brain.saveId.</summary>
    Npc = 2,

    /// <summary>A door: its scene save id.</summary>
    Door = 3,

    /// <summary>A scene object with no id of its own: a hash of where it sits in the scene.</summary>
    Path = 4,

    /// <summary>A scene object the game saves under its own id (light switches, sinks, the stove): its SaveID.</summary>
    Static = 5,
}

/// <summary>Names one networked thing the same way on every machine. Ids are only unique per kind.</summary>
public readonly struct NetRef : IEquatable<NetRef>
{
    public readonly NetKind Kind;
    public readonly int Id;

    public NetRef(NetKind kind, int id)
    {
        Kind = kind;
        Id = id;
    }

    public long Key => ((long)Kind << 32) | (uint)Id;

    public bool IsValid => Kind != NetKind.None;

    /// <summary>Items and NPCs come and go; doors and scene objects are always there.</summary>
    public bool IsDynamic => Kind == NetKind.Item || Kind == NetKind.Npc;

    public void Write(NetDataWriter writer)
    {
        writer.Put((byte)Kind);
        writer.Put(Id);
    }

    public static NetRef Read(NetDataReader reader) => new NetRef((NetKind)reader.GetByte(), reader.GetInt());

    public static NetRef FromKey(long key) => new NetRef((NetKind)(key >> 32), (int)(uint)key);

    public bool Equals(NetRef other) => Kind == other.Kind && Id == other.Id;

    public override bool Equals(object obj) => obj is NetRef other && Equals(other);

    public override int GetHashCode() => Key.GetHashCode();

    public override string ToString() => $"{Kind} {Id}";
}
