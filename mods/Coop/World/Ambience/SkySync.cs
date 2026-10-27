using System.Collections;
using System.Collections.Generic;
using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Ambience;

/// <summary>
/// The sky: the sun and moon turn a fixed step per frame (SunRotation), so they drift apart between
/// games running at different frame rates, and thunder strikes at random times in each game. The
/// host sends the lights' rotations twice a second and every strike as it starts; clients ease their
/// lights toward the host's and play the host's strikes instead of their own.
///
/// WorldSky: Lights (count, then a rotation each) or Strike, host to clients.
/// </summary>
internal static class SkySync
{
    private const byte Lights = 0;
    private const byte Strike = 1;
    private const float SendEvery = 0.5f;
    // How fast a client's light catches up with the host's, per second.
    private const float CatchUp = 2f;

    private static readonly List<Transform> Bodies = new List<Transform>();
    private static readonly List<Quaternion> Targets = new List<Quaternion>();
    private static UnityEngine.SceneManagement.Scene _scannedFor;
    private static float _next;
    private static bool _haveTargets;
    private static bool _replaying;

    public static void Init() => Messages.On(MessageIds.WorldSky, OnSky);

    public static void Clear()
    {
        _haveTargets = false;
        _scannedFor = default;
    }

    public static void LateTick(bool host)
    {
        if (!Scan())
            return;
        if (host)
            HostSend();
        else if (_haveTargets)
            Follow();
    }

    /// <summary>For the thunder patch: the coroutine about to start is a strike the host sent.</summary>
    public static bool TakeReplay()
    {
        bool replay = _replaying;
        _replaying = false;
        return replay;
    }

    /// <summary>Wraps a thunder coroutine: the host announces the strike when its wait is over; a client only plays the host's.</summary>
    public static IEnumerator Wrap(IEnumerator thunder, bool replay)
    {
        int step = 0;
        while (true)
        {
            if (step == 1 && WorldSide.Running)
            {
                if (WorldSide.IsHost)
                    Outbox.ToPlayers(Begin(Strike), Channel.Events);
                else if (!replay)
                    yield break; // The host's strikes come instead.
            }
            if (!thunder.MoveNext())
                yield break;
            step++;
            yield return thunder.Current;
        }
    }

    private static void HostSend()
    {
        if (Time.time < _next)
            return;
        _next = Time.time + SendEvery;
        var w = Begin(Lights);
        w.Put((byte)Bodies.Count);
        foreach (var body in Bodies)
        {
            var q = body.rotation;
            w.Put(q.x);
            w.Put(q.y);
            w.Put(q.z);
            w.Put(q.w);
        }
        Outbox.ToPlayers(w, Channel.Stream);
    }

    private static void Follow()
    {
        float t = 1f - Mathf.Exp(-CatchUp * Time.deltaTime);
        for (int i = 0; i < Bodies.Count && i < Targets.Count; i++)
            Bodies[i].rotation = Quaternion.Slerp(Bodies[i].rotation, Targets[i], t);
    }

    private static void OnSky(in Sender from, NetDataReader reader)
    {
        if (!WorldSide.Running || WorldSide.IsHost)
            return;
        byte kind = reader.GetByte();
        if (kind == Strike)
        {
            var house = HouseManager._;
            if (house != null && !house.noThunder)
            {
                _replaying = true;
                house.StartCoroutine(house.Thunder(instant: true));
                _replaying = false;
            }
            return;
        }
        int count = reader.GetByte();
        Targets.Clear();
        for (int i = 0; i < count; i++)
        {
            var q = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            Targets.Add(Valid(q) ? q.normalized : Quaternion.identity);
        }
        _haveTargets = Targets.Count > 0;
    }

    private static bool Valid(Quaternion q)
    {
        float length = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        return length > 0.25f && length < 2.25f;
    }

    // The sun and moon of this scene, in a stable order (by name) on every machine.
    private static bool Scan()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (_scannedFor == scene && Bodies.Count > 0 && Bodies[0] != null)
            return true;
        _scannedFor = scene;
        Bodies.Clear();
        foreach (var light in Object.FindObjectsOfType<SunRotation>())
            Bodies.Add(light.transform);
        Bodies.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return Bodies.Count > 0;
    }

    private static NetDataWriter Begin(byte kind)
    {
        var w = Outbox.Begin(MessageIds.WorldSky);
        w.Put(kind);
        return w;
    }
}
