using Coop.Session;
using Coop.World.Economy;
using Coop.World.Forwarding;
using Coop.World.Hostiles;
using Coop.World.Hazards;
using Coop.World.Manifest;
using Coop.World.Npcs;
using Coop.World.Objects;
using Coop.World.Ownership;
using Coop.World.Patches;
using Coop.World.Simulation;
using Coop.World.Spawning;
using Coop.World.State;
using Coop.World.Streaming;
using Coop.World.Talk;
using NeetModKit.Events;

namespace Coop.World;

/// <summary>
/// World sync: makes the house, its items and its systems the same in every game of a session.
/// This wires the parts to the session and the frame. It starts once a game is in the shared house
/// (the host when it starts hosting, a client once the host's house has loaded, either after a hot
/// reload) and stops with the session or the scene.
/// </summary>
internal static class WorldSync
{
    private static bool _hosting;

    public static void Init(CoopMod mod)
    {
        SpawnApplier.Init();
        OwnershipNet.Init();
        BodyReceiver.Init();
        NpcPoseReceiver.Init();
        ManifestReader.Init();
        StateSync.Init();
        NpcClothes.Init();
        EconomySync.Init();
        FireSync.Init();
        CopVanSync.Init();
        Forwarder.Init();
        HealthReport.Init();
        TalkSync.Init();
        RemoteRules.Init(mod);

        CoopSession.Started += () => WorldPatches.Apply(CoopSession.IsClient);
        CoopSession.WorldReady += () => Start(resumed: false);
        CoopSession.Resumed += () =>
        {
            if (!WorldSide.Running)
                Start(resumed: true);
        };
        CoopSession.PlayerReady += OnPlayerReady;
        CoopSession.PlayerLeft += player =>
        {
            if (!WorldSide.Running || !WorldSide.IsHost)
                return;
            Departures.PlayerLeft(player.Slot);
            TalkSync.PlayerLeft(player.Slot);
        };
        CoopSession.Ended += _ => End();

        CoopLoop.Update += Update;
        CoopLoop.FixedUpdate += FixedUpdate;
        CoopLoop.LateUpdate += LateUpdate;
        CoopLoop.Unloading += Unloading;
        GameEvents.SceneLoaded.Subscribe(mod, _ => Stop());

        // A session carried over a hot reload: the gates have to be up before anything else happens.
        if (CoopSession.IsActive)
            WorldPatches.Apply(CoopSession.IsClient);
    }

    private static void Start(bool resumed)
    {
        if (!GameEvents.IsHouseReady)
        {
            CoopMod.Log.Warn("World: the session is in the world but the house isn't ready; not starting world sync.");
            return;
        }
        Stop();
        WorldPatches.Apply(CoopSession.IsClient);
        _hosting = CoopSession.IsHost;
        SceneScan.Rebuild();
        StateTrackers.Rebuild();
        int carried = resumed ? OwnershipParking.Restore() : 0;
        if (!resumed)
            OwnershipParking.Forget();

        if (WorldSide.IsClient)
            StartClient();
        TalkSync.Start(_hosting);
        WorldSide.Running = true;
        NpcPoseSender.SendAllAgain();
        CoopMod.Log.Info($"World: running as {(_hosting ? "host" : "client")}" + (resumed ? $", {carried} owners carried over the reload." : "."));
    }

    // Everything the host simulates is a replica here, NPCs are puppets, and the house's own events are off.
    private static void StartClient()
    {
        IdRanges.UseLocalBlock(IdRanges.NextFor(WorldSide.LocalSlot));
        ClientSwitches.Apply();
        foreach (var o in NetObjects.All)
        {
            if (o.Brain != null || o.IsReplica)
                continue;
            if (o.Ref.IsDynamic && IdRanges.IsLocal(o.Ref.Id))
                o.Owner = WorldSide.LocalSlot;
            if (o.Owner != WorldSide.LocalSlot)
                ReplicaBody.MakeReplica(o);
        }
    }

    private static void OnPlayerReady(CoopPlayer player)
    {
        if (!WorldSide.Running || !WorldSide.IsHost)
            return;
        ManifestWriter.Send(player);
        EconomySync.SendTo(player.Slot);
        FireSync.SendAgain();
        CopVanSync.SendAgain();
        NpcPoseSender.SendAllAgain();
        NpcClothes.SendAllAgain();
        TalkSync.PlayerReady();
    }

    private static void End()
    {
        if (WorldSide.Running && _hosting)
            Departures.TakeEverythingBack();
        Stop();
        WorldPatches.Remove();
        OwnershipParking.Forget();
    }

    private static void Stop()
    {
        WorldSide.Running = false;
        TalkSync.Stop();
        Hitman.Clear();
        CopExits.Clear();
        NetObjects.Clear();
        StateTrackers.Clear();
        StateSync.Clear();
        EconomySync.Clear();
        FireSync.Clear();
        CopVanSync.Clear();
        RemoteNeeds.Clear();
        HealthReport.Clear();
        SpawnSender.Clear();
        SpawnContext.Reset();
        OwnershipNet.Clear();
        ReplicaDriver.Clear();
        NpcPoseReceiver.Clear();
        NpcPuppets.Forget();
        ContactClaims.Reset();
    }

    private static void Update()
    {
        if (!WorldSide.Running)
            return;
        long started = WorldTiming.Begin();
        SpawnSender.ProcessStartedBrains();
        NpcPuppets.ProcessLater();
        EconomySync.Update();
        TalkSync.Update(_hosting);
        WorldTiming.End(WorldTiming.Update, started);
    }

    private static void FixedUpdate()
    {
        if (!WorldSide.Running)
            return;
        long started = WorldTiming.Begin();
        ContactClaims.FixedTick();
        BodyStream.FixedTick();
        ReplicaDriver.FixedTick();
        RestWatcher.FixedTick();
        if (!_hosting)
            NpcPoseReceiver.FixedTick();
        WorldTiming.End(WorldTiming.Fixed, started);
    }

    // Late: the frame's spawns are set up by now. Spawns go first so claims and destroys never beat them.
    private static void LateUpdate()
    {
        if (!WorldSide.Running)
            return;
        long started = WorldTiming.Begin();
        if (_hosting)
        {
            NpcPoseSender.LateTick();
            NpcClothes.Tick();
            EconomySync.HostLateUpdate();
            FireSync.HostTick();
            CopVanSync.HostTick();
            RemoteNeeds.HostTick();
            TalkSync.HostLateUpdate();
            Hitman.Tick();
        }
        else
            NpcPoseReceiver.LateTick();
        HealthReport.Tick();
        SpawnSender.FlushSpawns();
        EconomySync.AfterSpawns();
        OwnershipNet.Flush();
        StateSync.LateTick();
        SpawnSender.FlushDestroys();
        WorldTiming.End(WorldTiming.Late, started);
    }

    private static void Unloading()
    {
        if (WorldSide.Running && CoopSession.IsActive)
            OwnershipParking.Park();
        NpcPuppets.RestoreDamage();
        Purchases.Unload();
        TalkExternals.Uninstall();
        WorldSide.Running = false;
    }
}
