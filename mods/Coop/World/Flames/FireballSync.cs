using System.Collections.Generic;
using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Flames;

/// <summary>
/// A spray lit by a flame (a lighter at the can's nozzle, a candle) becomes a ball of fire that rises
/// and burns for a few seconds. The balls aren't shared items: each is made by the game of the player
/// who sprayed. That game tells the others when a ball catches fire; they make the same ball where it
/// is (the game's own sphere, set alight, moving the same way) and drop it when the first one is gone.
///
/// What a ball does stays with the game that made it: it scorches paint and lights furniture there,
/// and a copy does neither (it can't paint, and it lights only things that are simulated in its own
/// game, like any flame that is only a copy: FlamePatches). A copy burns whoever stands in it in
/// their own game, so a player is burnt once, and the host's copy of a client's ball burns the
/// host's NPCs, which the client only has as puppets.
///
/// WorldFireball (any game, passed on by the host): sender slot, count, then per ball: id (ushort,
/// the sender's own), kind (byte, a known sphere or 255 followed by its name), position, velocity.
/// WorldFireballGone (any game, passed on by the host): sender slot, count, then the ids.
/// </summary>
internal static class FireballSync
{
    private const float CopyLives = 8f;
    private const byte OtherKind = 255;

    // The spheres a spray can shoot that have a flame, by the byte they go by.
    private static readonly string[] Kinds =
    {
        "SpraySphere_Cleaner",
        "SpraySphere_Cleaner_Grav",
        "SpraySphere_HairSpray",
        "SpraySphere_Hygiene_Increaser",
    };

    private sealed class Ball
    {
        public ushort Id;
        public int FlameId;
        public byte Slot;
        public Flame Flame;
        public Rigidbody Body;
        public string Prefab;
        public bool Told;
        public float Until;
    }

    private static readonly List<Ball> Mine = new List<Ball>(16);
    private static readonly Dictionary<int, Ball> Copies = new Dictionary<int, Ball>(16);
    private static readonly Dictionary<long, Ball> CopyByKey = new Dictionary<long, Ball>(16);
    private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>(8);
    private static readonly HashSet<int> Known = new HashSet<int>();
    private static readonly List<int> Expired = new List<int>(4);
    private static ushort _next;

    public static void Init()
    {
        Messages.On(MessageIds.WorldFireball, OnFireball);
        Messages.On(MessageIds.WorldFireballGone, OnGone);
    }

    public static void Clear()
    {
        foreach (var copy in Copies.Values)
        {
            if (copy.Flame != null)
                Object.Destroy(copy.Flame.gameObject);
        }
        Mine.Clear();
        Copies.Clear();
        CopyByKey.Clear();
        Known.Clear();
        Expired.Clear();
        Prefabs.Clear();
    }

    /// <summary>For the tests: balls made here that are burning, and copies of other games' balls.</summary>
    public static string Counts() => $"{Mine.Count} made here, {Copies.Count} copies";

    /// <summary>This flame is the copy of another game's ball.</summary>
    public static bool IsCopy(Flame flame) => Copies.Count > 0 && Copies.ContainsKey(flame.GetInstanceID());

    /// <summary>From the hook on <c>Flame.Ignite</c>: a flame was lit here.</summary>
    public static void Lit(Flame flame)
    {
        if (!WorldSide.Running || !flame.lit || !flame.TryGetComponent(out SpraySphere _))
            return;
        if (!Known.Add(flame.GetInstanceID()))
            return;
        string prefab = flame.gameObject.name;
        int clone = prefab.IndexOf("(Clone)", System.StringComparison.Ordinal);
        if (clone > 0)
            prefab = prefab.Substring(0, clone);
        flame.TryGetComponent(out Rigidbody body);
        Mine.Add(new Ball { Id = ++_next, FlameId = flame.GetInstanceID(), Slot = WorldSide.LocalSlot, Flame = flame, Body = body, Prefab = prefab });
    }

    /// <summary>End of the frame: tells about new balls and balls that are gone, and drops old copies.</summary>
    public static void LateTick()
    {
        if (Mine.Count > 0)
            SendMine();
        if (Copies.Count > 0)
            DropOldCopies();
    }

    private static void SendMine()
    {
        int fresh = 0;
        int gone = 0;
        foreach (var ball in Mine)
        {
            if (!ball.Told)
                fresh++;
            if (Over(ball))
                gone++;
        }
        if (fresh > 0)
            SendFresh(fresh);
        if (gone > 0)
            SendGone(gone);
        for (int i = Mine.Count - 1; i >= 0; i--)
        {
            if (Over(Mine[i]))
            {
                Known.Remove(Mine[i].FlameId);
                Mine.RemoveAt(i);
            }
        }
    }

    // The ball burnt out, was put out or was destroyed (the game destroys it a few seconds after it lights).
    private static bool Over(Ball ball) => ball.Flame == null || !ball.Flame.lit;

    private static void SendFresh(int count)
    {
        var w = Outbox.Begin(MessageIds.WorldFireball);
        w.Put(WorldSide.LocalSlot);
        w.Put((byte)count);
        foreach (var ball in Mine)
        {
            if (ball.Told)
                continue;
            ball.Told = true;
            w.Put(ball.Id);
            int kind = System.Array.IndexOf(Kinds, ball.Prefab);
            w.Put(kind >= 0 ? (byte)kind : OtherKind);
            if (kind < 0)
                w.Put(ball.Prefab);
            Put(w, ball.Flame != null ? ball.Flame.transform.position : Vector3.zero);
            Put(w, ball.Body != null ? ball.Body.velocity : Vector3.zero);
        }
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void SendGone(int count)
    {
        var w = Outbox.Begin(MessageIds.WorldFireballGone);
        w.Put(WorldSide.LocalSlot);
        w.Put((byte)count);
        foreach (var ball in Mine)
        {
            if (Over(ball))
                w.Put(ball.Id);
        }
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void Put(NetDataWriter w, Vector3 v)
    {
        w.Put(v.x);
        w.Put(v.y);
        w.Put(v.z);
    }

    private static Vector3 Get(NetDataReader r) => new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());

    private static long KeyOf(byte slot, ushort id) => slot * 65536L + id;

    private static void OnFireball(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        if (WorldSide.IsHost)
            Relay.Forward(MessageIds.WorldFireball, reader.RawData, start, length, Channel.Events, from.Slot);
        byte slot = reader.GetByte();
        int count = reader.GetByte();
        for (int i = 0; i < count; i++)
        {
            ushort id = reader.GetUShort();
            byte kind = reader.GetByte();
            string prefab = kind == OtherKind ? reader.GetString() : kind < Kinds.Length ? Kinds[kind] : null;
            var at = Get(reader);
            var velocity = Get(reader);
            if (WorldSide.Running && prefab != null)
                MakeCopy(slot, id, prefab, at, velocity);
        }
    }

    private static void OnGone(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        if (WorldSide.IsHost)
            Relay.Forward(MessageIds.WorldFireballGone, reader.RawData, start, length, Channel.Events, from.Slot);
        byte slot = reader.GetByte();
        int count = reader.GetByte();
        for (int i = 0; i < count; i++)
        {
            long key = KeyOf(slot, reader.GetUShort());
            if (!CopyByKey.TryGetValue(key, out var copy))
                continue;
            Forget(key, copy);
            if (copy.Flame != null)
                Object.Destroy(copy.Flame.gameObject);
        }
    }

    // The same ball, made the way the game makes it and set alight: it can't paint, and being a copy
    // it lights only what is simulated here (FlameRules).
    private static void MakeCopy(byte slot, ushort id, string prefabName, Vector3 at, Vector3 velocity)
    {
        long key = KeyOf(slot, id);
        if (CopyByKey.ContainsKey(key) || !Prefabs.TryGetValue(prefabName, out var prefab) && (prefab = Load(prefabName)) == null)
            return;
        var go = Object.Instantiate(prefab, at, Quaternion.identity);
        var flame = go.GetComponentInChildren<Flame>(true);
        if (flame == null)
        {
            Object.Destroy(go);
            return;
        }
        foreach (var painter in go.GetComponentsInChildren<CollisionPainter>(true))
            painter.preventPainting = true;
        if (go.TryGetComponent(out Rigidbody body))
            body.velocity = velocity;
        var copy = new Ball { Id = id, FlameId = flame.GetInstanceID(), Slot = slot, Flame = flame, Body = body, Prefab = prefabName, Until = Time.time + CopyLives };
        Copies[copy.FlameId] = copy;
        CopyByKey[key] = copy;
        Known.Add(copy.FlameId);
        flame.Ignite(force: true);
    }

    private static GameObject Load(string name)
    {
        var prefab = Resources.Load<GameObject>(name);
        if (prefab != null)
            Prefabs[name] = prefab;
        return prefab;
    }

    private static void DropOldCopies()
    {
        float now = Time.time;
        Expired.Clear();
        foreach (var pair in Copies)
        {
            if (pair.Value.Flame == null || pair.Value.Until < now)
                Expired.Add(pair.Key);
        }
        foreach (int id in Expired)
        {
            var copy = Copies[id];
            Forget(KeyOf(copy.Slot, copy.Id), copy);
            if (copy.Flame != null)
                Object.Destroy(copy.Flame.gameObject);
        }
    }

    private static void Forget(long key, Ball copy)
    {
        CopyByKey.Remove(key);
        Copies.Remove(copy.FlameId);
        Known.Remove(copy.FlameId);
    }
}
