using System.Collections.Generic;
using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Co-op: a player whose health runs out goes down instead of dying. They fall like a slip and stay
/// there; for <see cref="BleedOutSeconds"/> anyone can bring them back by using food on them, and after
/// that the game's own death and respawn happen. Only a game's own player goes down; each game tells
/// the others, so their bodies show it and enemies can leave them alone (<see cref="IsDown"/>).
/// </summary>
internal static class Downed
{
    public const float BleedOutSeconds = 15f;
    private const float ReviveHealth = 30f;

    private static readonly HashSet<byte> Down = new HashSet<byte>();
    private static GameObject _player;
    private static PlayerStats _stats;
    private static CameraControl _camera;
    private static bool _wasInvincible;
    private static float _until;
    private static bool _dying;

    public static bool LocalDown => _player != null;

    public static bool IsDown(byte slot) => Down.Contains(slot);

    public static void Init()
    {
        Messages.On(MessageIds.Downed, OnDowned);
        Messages.On(MessageIds.Revive, OnRevive);
        CoopLoop.Update += Tick;
        CoopSession.PlayerLeft += player => Down.Remove(player.Slot);
        CoopSession.Ended += _ => Clear();
        CoopLoop.Unloading += Clear;
    }

    /// <summary>From the death hook: true when this death became going down.</summary>
    public static bool TryGoDown(Player_Abilities rig)
    {
        if (_dying || !CoopSession.InWorld || HouseManager._ == null || rig.gameObject != HouseManager._.player)
            return false;
        if (LocalDown)
            return true; // Already down: more damage changes nothing.

        _player = rig.gameObject;
        _until = Time.time + BleedOutSeconds;
        // Down, not dead: no death screen, and nothing hurts while waiting for help.
        _stats = rig.GetComponent<PlayerStats>();
        _camera = HouseManager._.mainCamera != null ? HouseManager._.mainCamera.GetComponent<CameraControl>() : null;
        if (_stats != null)
        {
            _wasInvincible = _stats.invincible;
            _stats.invincible = true;
            _stats.health.ForceSetVal(1f);
        }
        KeepEyesOpen();
        if (rig.TryGetComponent(out Player_Movement movement))
            movement.SlipAccessorNoRecov(true);
        Announce(true);
        CoopMod.Log.Info("You went down.");
        return true;
    }

    /// <summary>Someone used food on the player in <paramref name="slot"/>.</summary>
    public static void Revive(byte slot)
    {
        if (slot == CoopSession.Local?.Slot)
        {
            StandUp();
            return;
        }
        var message = Outbox.Begin(MessageIds.Revive);
        message.Put(slot);
        if (CoopSession.IsHost)
            Outbox.ToSlot(slot, message, Channel.Events);
        else
            Outbox.ToHost(message, Channel.Events);
    }

    private static void Tick()
    {
        if (!LocalDown)
            return;
        // Respawned or died some other way (gibbed, a story death) in the meantime.
        if (HouseManager._ == null || HouseManager._.player != _player || !_player.TryGetComponent(out Player_Abilities rig) || rig.dead)
        {
            Clear();
            return;
        }
        KeepEyesOpen();
        float left = _until - Time.time;
        DownedHud.Show(left);
        if (left <= 0f)
            BleedOut(rig);
    }

    // Knocks (falling, hits) shut the eyes, which shows the black screen with the needs on it.
    private static void KeepEyesOpen()
    {
        if (_stats != null && _stats.knockout.GetVal() > 0f)
            _stats.knockout.ForceSetVal(0f);
        if (_camera != null)
            _camera.eyesForcedShut = false;
    }

    private static void BleedOut(Player_Abilities rig)
    {
        Clear();
        _dying = true;
        try
        {
            rig.DieAccessor(false);
        }
        finally
        {
            _dying = false;
        }
    }

    private static void StandUp()
    {
        if (!LocalDown)
            return;
        var player = _player;
        var stats = _stats;
        Clear();
        if (stats != null)
            stats.health.ForceSetVal(ReviveHealth);
        if (player.TryGetComponent(out Player_Movement movement))
            movement.SlipRecoverAccessor();
        CoopMod.Log.Info("Someone fed you back up.");
    }

    private static void Clear()
    {
        if (_player != null && CoopSession.Local != null)
            Announce(false);
        if (_stats != null)
            _stats.invincible = _wasInvincible;
        _player = null;
        _stats = null;
        _camera = null;
        DownedHud.Hide();
    }

    private static void Announce(bool down)
    {
        byte slot = CoopSession.Local?.Slot ?? CoopPlayer.HostSlot;
        Set(slot, down);
        if (!CoopSession.InWorld)
            return;
        var message = Outbox.Begin(MessageIds.Downed);
        message.Put(slot);
        message.Put(down);
        Outbox.ToPlayers(message, Channel.Events);
    }

    private static void OnDowned(in Sender from, NetDataReader reader)
    {
        byte slot = reader.GetByte();
        bool down = reader.GetBool();
        if (CoopSession.IsHost)
        {
            slot = from.Slot;
            var relay = Outbox.Begin(MessageIds.Downed);
            relay.Put(slot);
            relay.Put(down);
            Outbox.ToPlayers(relay, Channel.Events, exceptSlot: slot);
        }
        Set(slot, down);
    }

    private static void OnRevive(in Sender from, NetDataReader reader)
    {
        byte slot = reader.GetByte();
        if (slot == CoopSession.Local?.Slot)
            StandUp();
        else if (CoopSession.IsHost)
            Revive(slot);
    }

    private static void Set(byte slot, bool down)
    {
        if (down)
            Down.Add(slot);
        else
            Down.Remove(slot);
        RemotePlayers.Find(slot)?.SetDowned(down);
    }
}
