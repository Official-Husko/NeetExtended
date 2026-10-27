using System.Collections.Generic;
using Coop.Net;
using Coop.World.Objects;
using HarmonyLib;
using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coop.World.Flames;

/// <summary>
/// The stove's gas. Turning the knob is shared state (StateSync). What the gas does when something
/// lights it is decided once, by the host: the flame, the bang, the push on loose things and the
/// fires it starts. A client's lighter (or fire) that touches the gas only asks the host; the host
/// lights it and tells everyone how much gas there was, and each game then plays the same ignition
/// with those numbers (the sound, the flame, the burning zone that hurts whoever stands in it, items
/// pushed in the games that simulate them). The fires it starts are the host's and arrive as a room
/// fire. A flame the water puts out goes out everywhere.
///
/// WorldGas: kind, stove id, then
/// - Ignited (host to clients): gas leaking, gas zone scale (3 floats), ignition part scale;
/// - Out (any game, passed on by the host): nothing more;
/// - Ask (client to host): nothing more.
/// </summary>
internal static class GasSync
{
    private const byte Ignited = 0;
    private const byte Out = 1;
    private const byte Ask = 2;
    private const float AskGap = 0.5f;

    private static readonly AccessTools.FieldRef<GasButton, Flame> FlameOf = AccessTools.FieldRefAccess<GasButton, Flame>("flame");

    private static readonly Dictionary<int, GasButton> ById = new Dictionary<int, GasButton>(8);
    private static readonly Dictionary<int, GasButton> ByFlame = new Dictionary<int, GasButton>(8);
    private static float _askAfter;

    /// <summary>True while a stove's ignition or putting out is being played from the host's word.</summary>
    public static bool Replaying { get; private set; }

    public static void Init() => Messages.On(MessageIds.WorldGas, OnGas);

    public static void Clear()
    {
        ById.Clear();
        ByFlame.Clear();
        Replaying = false;
    }

    public static void Rebuild()
    {
        Clear();
        var scene = SceneManager.GetActiveScene();
        foreach (var gas in Object.FindObjectsOfType<GasButton>(true))
        {
            if (gas.gameObject.scene != scene)
                continue;
            ById[gas.SaveID] = gas;
            var flame = FlameOf(gas) ?? gas.GetComponentInChildren<Flame>(true);
            if (flame != null)
                ByFlame[flame.GetInstanceID()] = gas;
            if (gas.gasZone != null && gas.gasZone.TryGetComponent(out Flame zone))
                ByFlame[zone.GetInstanceID()] = gas;
        }
    }

    public static IEnumerable<GasButton> Stoves => ById.Values;

    public static Flame FlameOfStove(GasButton gas) => FlameOf(gas);

    /// <summary>The flame is a stove's (the burner flame or its gas zone).</summary>
    public static bool IsStove(Flame flame) => ByFlame.ContainsKey(flame.GetInstanceID());

    /// <summary>Client: something lit here touched the stove's gas; the host decides whether it lights.</summary>
    public static void AskToLight(Flame flame)
    {
        if (!WorldSide.IsClient || !ByFlame.TryGetValue(flame.GetInstanceID(), out var gas))
            return;
        float now = Time.unscaledTime;
        if (now < _askAfter)
            return;
        _askAfter = now + AskGap;
        var w = Begin(Ask, gas);
        Outbox.ToHost(w, Channel.Events);
    }

    /// <summary>Host, as the gas lights (before the game plays it): everyone plays it with the same gas.</summary>
    public static void Lighting(GasButton gas)
    {
        if (!WorldSide.Running || !WorldSide.IsHost || Replaying)
            return;
        var w = Begin(Ignited, gas);
        w.Put(gas.gasLeaking);
        var zone = gas.gasZone.transform.localScale;
        w.Put(zone.x);
        w.Put(zone.y);
        w.Put(zone.z);
        w.Put(gas.ignitePart.transform.localScale.x);
        Outbox.ToPlayers(w, Channel.Events);
    }

    /// <summary>The stove's flame went out by itself (water, a spray), not by turning the knob off.</summary>
    public static void PutOut(GasButton gas)
    {
        if (!WorldSide.Running || Replaying || !gas.gasLeaking)
            return;
        Outbox.ToPlayers(Begin(Out, gas), Channel.Events);
    }

    private static NetDataWriter Begin(byte kind, GasButton gas)
    {
        var w = Outbox.Begin(MessageIds.WorldGas);
        w.Put(kind);
        w.Put(gas.SaveID);
        return w;
    }

    private static void OnGas(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        byte kind = reader.GetByte();
        int id = reader.GetInt();
        if (WorldSide.IsHost && kind == Out)
            Relay.Forward(MessageIds.WorldGas, reader.RawData, start, length, Channel.Events, from.Slot);
        if (!WorldSide.Running || !ById.TryGetValue(id, out var gas) || gas == null)
            return;
        var flame = FlameOf(gas);
        if (flame == null)
            return;
        switch (kind)
        {
            case Ignited when !WorldSide.IsHost:
                Replay(gas, flame, reader);
                break;
            case Out:
                Replaying = true;
                try
                {
                    flame.Extinguish();
                }
                finally
                {
                    Replaying = false;
                }
                break;
            case Ask when WorldSide.IsHost:
                if (!flame.lit && !flame.preventIgnition)
                    flame.Ignite();
                break;
        }
    }

    // The same ignition the host just had: the knob, the gas as it was, then the game's own lighting.
    private static void Replay(GasButton gas, Flame flame, NetDataReader reader)
    {
        bool leaking = reader.GetBool();
        var zone = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        float part = reader.GetFloat();
        Replaying = true;
        try
        {
            if (gas.gasLeaking != leaking)
                gas.Toggle();
            gas.gasZone.transform.localScale = zone;
            gas.ignitePart.transform.localScale = Vector3.one * part;
            flame.Ignite(force: true);
        }
        finally
        {
            Replaying = false;
        }
    }
}
