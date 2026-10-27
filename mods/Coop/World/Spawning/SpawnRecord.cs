using System;
using Coop.World.Objects;
using LiteNetLib.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace Coop.World.Spawning;

/// <summary>Everything another game needs to make the same thing: what, where, whose, and its parts' state.</summary>
internal struct SpawnRecord
{
    public NetRef Ref;
    public byte Owner;
    public string PrefabName;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
    public byte Flags;
    public Vector3 Velocity;

    /// <summary>The item's component saves (cooked, filled, box content...) as the game's own JSON; may be empty.</summary>
    public string SubSaves;

    public static SpawnRecord Capture(NetObject o)
    {
        var record = new SpawnRecord
        {
            Ref = o.Ref,
            Owner = o.Owner,
            PrefabName = o.PrefabName,
            Position = o.Tf.position,
            Rotation = o.Tf.rotation,
            Scale = o.Tf.localScale,
            Flags = BodyFlags.Of(o),
            SubSaves = "",
        };
        if (o.Body != null && !o.Body.isKinematic)
            record.Velocity = o.Body.velocity;
        if (o.Ref.Kind == NetKind.Item && o.Usable != null)
            record.SubSaves = SubSavesOf(o.Usable);
        return record;
    }

    // A cold path: once per spawn. Usable.Save builds the same list the save file gets.
    private static string SubSavesOf(Usable usable)
    {
        try
        {
            var save = usable.Save();
            if (save?.subSaveData == null || save.subSaveData.Count == 0)
                return "";
            return JsonConvert.SerializeObject(save.subSaveData);
        }
        catch (Exception e)
        {
            CoopMod.Log.Debug($"World: couldn't read the state of {usable.name} for its spawn: {e.Message}");
            return "";
        }
    }

    public void Write(NetDataWriter writer)
    {
        Ref.Write(writer);
        writer.Put(Owner);
        writer.Put(PrefabName ?? "");
        Put(writer, Position);
        writer.Put(Rotation.x);
        writer.Put(Rotation.y);
        writer.Put(Rotation.z);
        writer.Put(Rotation.w);
        Put(writer, Scale);
        writer.Put(Flags);
        Put(writer, Velocity);
        writer.Put(SubSaves ?? "");
    }

    public static SpawnRecord Read(NetDataReader reader) => new SpawnRecord
    {
        Ref = NetRef.Read(reader),
        Owner = reader.GetByte(),
        PrefabName = reader.GetString(),
        Position = Get(reader),
        Rotation = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat()),
        Scale = Get(reader),
        Flags = reader.GetByte(),
        Velocity = Get(reader),
        SubSaves = reader.GetString(),
    };

    private static void Put(NetDataWriter writer, Vector3 v)
    {
        writer.Put(v.x);
        writer.Put(v.y);
        writer.Put(v.z);
    }

    private static Vector3 Get(NetDataReader reader) => new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
}
