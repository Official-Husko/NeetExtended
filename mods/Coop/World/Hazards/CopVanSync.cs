using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Hazards;

/// <summary>
/// The cop van. Something a client does can call it (ordering black-market goods, a box's contents,
/// overdue taxes); the call goes to the host, which sends the van (one at a time, as in single
/// player). Clients see the host's van parked outside; it doesn't send cops there, the host's cops
/// arrive as NPCs.
///
/// WorldCopVan: Call (client to host), Shown or Gone (host to all).
/// </summary>
internal static class CopVanSync
{
    private const byte Call = 0;
    private const byte Shown = 1;
    private const byte Gone = 2;
    private const float Interval = 1f;

    // Where HouseManager.SpawnCopVan parks it.
    private static readonly Vector3 Parking = new Vector3(-13.02f, -0.13f, 6.3f);

    private static bool _shown;
    private static float _next;

    public static void Init() => Messages.On(MessageIds.WorldCopVan, OnVan);

    public static void Clear() => _shown = false;

    public static void SendAgain() => _shown = false;

    /// <summary>Client, instead of spawning the van here.</summary>
    public static void RequestFromHost()
    {
        if (!WorldSide.Running)
            return;
        var w = Outbox.Begin(MessageIds.WorldCopVan);
        w.Put(Call);
        Outbox.ToHost(w, Channel.Events);
    }

    /// <summary>Host, once a second: the van came or went.</summary>
    public static void HostTick()
    {
        float now = Time.time;
        if (now < _next || HouseManager._ == null)
            return;
        _next = now + Interval;
        bool there = HouseManager._.copVan != null;
        if (there == _shown)
            return;
        _shown = there;
        var w = Outbox.Begin(MessageIds.WorldCopVan);
        w.Put(there ? Shown : Gone);
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void OnVan(in Sender from, NetDataReader reader)
    {
        byte what = reader.GetByte();
        var house = HouseManager._;
        if (!WorldSide.Running || house == null)
            return;
        if (WorldSide.IsHost)
        {
            if (what == Call)
                house.SpawnCopVan();
            return;
        }
        if (what == Shown && house.copVan == null)
        {
            var prefab = Resources.Load<GameObject>("CopVan");
            if (prefab == null)
                return;
            house.copVan = Object.Instantiate(prefab, Parking, Quaternion.identity);
            // Only the host's van sends cops.
            if (house.copVan.TryGetComponent(out CopVan van))
                van.enabled = false;
        }
        else if (what == Gone && house.copVan != null)
        {
            Object.Destroy(house.copVan);
            house.copVan = null;
        }
    }
}
