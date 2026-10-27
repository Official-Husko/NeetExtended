using System;
using Coop.Net;
using HarmonyLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Economy;

/// <summary>
/// Buying on a client's computer is a request the host carries out with the game's own method:
/// store orders (every store, the custom shops and items mods put in the stores) through
/// <c>Computer.OrderItem</c>, apps through <c>AppDownloader.BuyApp</c>. So the money check, the demo
/// block, the cop van roll and the box at the drop-off all happen once, on the host, against the one
/// wallet. The host answers, and the client plays the order's confirm or cancel sound.
///
/// WorldPurchase (client to host): kind, then
/// - Order: item name, price offset, own delivery spot (flag, position, rotation), custom kind, file path;
/// - App: the app's index.
/// WorldPurchaseResult (host to the buyer): kind, went through.
/// </summary>
internal static class Purchases
{
    private const byte Order = 0;
    private const byte App = 1;

    // The game's own wait between two orders (Computer.OrderItem), kept per client.
    private const float OrderCooldown = 0.5f;

    private static readonly AccessTools.FieldRef<Computer, float> LastOrder =
        AccessTools.FieldRefAccess<Computer, float>("lastOrderTimestamp");

    private static float _lastOrder = float.NegativeInfinity;
    private static Transform _spot;

    public static void Init()
    {
        Messages.On(MessageIds.WorldPurchase, OnRequest);
        Messages.On(MessageIds.WorldPurchaseResult, OnResult);
    }

    public static void Clear() => _lastOrder = float.NegativeInfinity;

    /// <summary>Before the mod unloads: its helper object goes with it.</summary>
    public static void Unload()
    {
        if (_spot != null)
            UnityEngine.Object.Destroy(_spot.gameObject);
        _spot = null;
    }

    /// <summary>
    /// Client, before <c>Computer.OrderItem</c>: false keeps the order from running here. The item is
    /// cleared for the patches after this one, so mods that change orders leave it to the host's run.
    /// </summary>
    public static bool BeforeOrder(Computer computer, ref GameObject obj, Transform deliverySpot, float priceOffset, Action<GameObject> onUnbox)
    {
        if (!WorldSide.Running || !WorldSide.IsClient || obj == null || computer != HouseManager._?.mainComputer)
            return true;
        var item = obj;
        obj = null;
        float now = Time.time;
        if (now <= _lastOrder + OrderCooldown)
        {
            PlayOrder(computer, false);
            return false;
        }
        _lastOrder = now;

        var w = Outbox.Begin(MessageIds.WorldPurchase);
        w.Put(Order);
        w.Put(item.name);
        w.Put(priceOffset);
        bool ownSpot = deliverySpot != null && deliverySpot != HouseManager._.packageDropOffPoint;
        w.Put(ownSpot);
        if (ownSpot)
        {
            Put(w, deliverySpot.position);
            var r = deliverySpot.rotation;
            w.Put(r.x);
            w.Put(r.y);
            w.Put(r.z);
            w.Put(r.w);
        }
        BoxCustomization.Describe(onUnbox, out byte kind, out string path);
        w.Put(kind);
        w.Put(path);
        Outbox.ToHost(w, Channel.Events);
        return false;
    }

    /// <summary>Client, before <c>AppDownloader.BuyApp</c>: false keeps the purchase from running here.</summary>
    public static bool BeforeApp(int appId, bool enableOnly)
    {
        // enableOnly is the game showing apps a save already owns: that stays local.
        if (enableOnly || !WorldSide.Running || !WorldSide.IsClient || appId < 0 || appId > byte.MaxValue)
            return true;
        var w = Outbox.Begin(MessageIds.WorldPurchase);
        w.Put(App);
        w.Put((byte)appId);
        Outbox.ToHost(w, Channel.Events);
        return false;
    }

    private static void OnRequest(in Sender from, NetDataReader reader)
    {
        if (!WorldSide.IsHost || !WorldSide.Running)
            return;
        byte kind = reader.GetByte();
        bool done;
        try
        {
            done = kind == Order ? RunOrder(reader) : kind == App && RunApp(reader.GetByte());
        }
        catch (Exception e)
        {
            CoopMod.Log.Warn($"World: player {from.Slot}'s purchase failed here: {e.Message}");
            done = false;
        }
        var w = Outbox.Begin(MessageIds.WorldPurchaseResult);
        w.Put(kind);
        w.Put(done);
        Outbox.ToSlot(from.Slot, w, Channel.Events);
    }

    private static bool RunOrder(NetDataReader r)
    {
        string name = r.GetString();
        float priceOffset = r.GetFloat();
        Transform spot = null;
        if (r.GetBool())
        {
            spot = Spot();
            spot.SetPositionAndRotation(Get(r), new Quaternion(r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetFloat()));
        }
        byte custom = r.GetByte();
        string path = r.GetString();

        var computer = HouseManager._?.mainComputer;
        var item = Prefab(name);
        if (computer == null || item == null)
        {
            CoopMod.Log.Debug($"World: a client ordered {name}, which isn't an item here.");
            return false;
        }
        var onUnbox = custom != BoxCustomization.None ? new BoxCustomization(custom, path).AsDelegate() : null;
        // The host's own wait between orders is for its own double clicks; each client has its own.
        float lastHere = LastOrder(computer);
        LastOrder(computer) = float.NegativeInfinity;
        GameObject ordered = null;
        try
        {
            ordered = computer.OrderItem(item, spot, priceOffset, onUnbox);
        }
        finally
        {
            if (ordered == null)
                LastOrder(computer) = lastHere;
            // The buyer plays the order's sound at their own screen.
            computer.GetComponents<AudioSource>()[1].Stop();
        }
        return ordered != null;
    }

    private static bool RunApp(int appId)
    {
        var apps = AppSync.Downloader();
        if (apps == null || apps.boughtApps == null || appId >= apps.boughtApps.Length || apps.boughtApps[appId])
            return false;
        apps.BuyApp(appId);
        apps.GetComponent<AudioSource>().Stop();
        return apps.boughtApps[appId];
    }

    private static void OnResult(in Sender from, NetDataReader reader)
    {
        byte kind = reader.GetByte();
        bool done = reader.GetBool();
        if (!WorldSide.Running || WorldSide.IsHost)
            return;
        if (kind == Order)
        {
            var computer = HouseManager._?.mainComputer;
            if (computer != null)
                PlayOrder(computer, done);
        }
        else if (kind == App)
        {
            var apps = AppSync.Downloader();
            if (apps != null && apps.clips != null && apps.clips.Length > 1)
            {
                var source = apps.GetComponent<AudioSource>();
                source.clip = apps.clips[done ? 0 : 1];
                source.Play();
            }
        }
    }

    private static void PlayOrder(Computer computer, bool done)
    {
        var source = computer.GetComponents<AudioSource>()[1];
        source.clip = done ? computer.orderConfirmClip : computer.orderCancelClip;
        source.Play();
    }

    // Store items by the name saves use; items only a store's own list has are looked up there (cold path).
    private static GameObject Prefab(string name)
    {
        if (PickupBank.pickupsDict.TryGetValue(name, out var known) && known.obj != null)
            return known.obj;
        foreach (var bank in Resources.FindObjectsOfTypeAll<PickupBank>())
        {
            if (bank.pickups == null)
                continue;
            foreach (var pickup in bank.pickups)
            {
                if (pickup.obj != null && pickup.obj.name == name)
                    return pickup.obj;
            }
        }
        return null;
    }

    // A client's own delivery spot, for the game's OrderItem which wants a transform.
    private static Transform Spot()
    {
        if (_spot == null)
        {
            var go = new GameObject("Coop delivery spot") { hideFlags = HideFlags.HideAndDontSave };
            _spot = go.transform;
        }
        return _spot;
    }

    private static void Put(NetDataWriter w, Vector3 v)
    {
        w.Put(v.x);
        w.Put(v.y);
        w.Put(v.z);
    }

    private static Vector3 Get(NetDataReader r) => new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
}
