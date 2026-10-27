using Coop.Session;
using Coop.World.Economy;
using Coop.World.Manifest;
using Coop.World.Npcs;
using Coop.World.Objects;
using Coop.World.Ownership;
using Coop.World.Patches;
using Coop.World.Simulation;
using Coop.World.Spawning;
using Coop.World.State;
using Coop.World.Streaming;
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
        MoneySync.Init();
        ManifestReader.Init();
        StateSync.Init();
        NpcClothes.Init();

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
            if (WorldSide.Running && WorldSide.IsHost)
                Departures.PlayerLeft(player.Slot);
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
        MoneySync.Reset();
        WorldPatches.Apply(CoopSession.IsClient);
        _hosting = CoopSession.IsHost;
        SceneScan.Rebuild();
        StateTrackers.Rebuild();
        int carried = resumed ? OwnershipParking.Restore() : 0;
        if (!resumed)
            OwnershipParking.Forget();

        if (WorldSide.IsClient)
            StartClient();
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
        MoneySync.SendTo(player.Slot);
        NpcPoseSender.SendAllAgain();
        NpcClothes.SendAllAgain();
    }

    private static void End()
    {
        if (WorldSide.Running && _hosting)
            Departures.TakeEverythingBack();
        Stop();
        WorldPatches.Remove();
        MoneySync.Reset();
        OwnershipParking.Forget();
    }

    private static void Stop()
    {
        WorldSide.Running = false;
        NetObjects.Clear();
        StateTrackers.Clear();
        StateSync.Clear();
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
        SpawnSender.ProcessStartedBrains();
        NpcPuppets.ProcessLater();
        MoneySync.Check();
    }

    private static void FixedUpdate()
    {
        if (!WorldSide.Running)
            return;
        ContactClaims.FixedTick();
        BodyStream.FixedTick();
        ReplicaDriver.FixedTick();
        RestWatcher.FixedTick();
        if (!_hosting)
            NpcPoseReceiver.FixedTick();
    }

    // Late: the frame's spawns are set up by now. Spawns go first so claims and destroys never beat them.
    private static void LateUpdate()
    {
        if (!WorldSide.Running)
            return;
        if (_hosting)
        {
            NpcPoseSender.LateTick();
            NpcClothes.Tick();
        }
        else
            NpcPoseReceiver.LateTick();
        SpawnSender.FlushSpawns();
        OwnershipNet.Flush();
        StateSync.LateTick();
        SpawnSender.FlushDestroys();
    }

    private static void Unloading()
    {
        if (WorldSide.Running && CoopSession.IsActive)
            OwnershipParking.Park();
        NpcPuppets.RestoreDamage();
        WorldSide.Running = false;
    }
}
