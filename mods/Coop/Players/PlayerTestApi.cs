using Coop.Session;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Coop.Players;

/// <summary>
/// Test entry points for going down, reviving, carrying and moves done to a body, called from the dev
/// bridge: <c>neet call type.call type=Coop.Players.PlayerTestApi method=Kill args=["gib"] --instance 2</c>.
/// They run the game's own methods, so the hooks see what they'd see in play.
/// </summary>
public static class PlayerTestApi
{
    /// <summary>Kill this game's player the way <paramref name="how"/> says: die, skeleton, hollow, gib, damage.</summary>
    public static string Kill(string how)
    {
        var player = HouseManager._?.player;
        if (player == null || !player.TryGetComponent(out Player_Abilities rig))
            return "no player";
        switch (how)
        {
            case "die": rig.DieAccessor(); break;
            case "skeleton": rig.SkeletonizeAccessor(); break;
            case "hollow": rig.SkeletonizeAccessor(true); break;
            case "gib": rig.Obliterate(); break;
            case "damage": player.GetComponent<PlayerStats>().health.Affect(player.transform.position, -500f, EffectType.Impact); break;
            default: return "unknown: " + how;
        }
        return Status();
    }

    /// <summary>Whether this game's player is down, which slots it thinks are down, who is carried.</summary>
    public static string Status()
    {
        var down = new System.Text.StringBuilder();
        foreach (var player in CoopSession.Players)
        {
            if (Downed.IsDown(player.Slot))
                down.Append(player.Slot).Append(' ');
        }
        return $"localDown={Downed.LocalDown} carried={Carrying.LocalCarried} carrying={Carrying.Carried} down=[{down}] dead={Dead()}";
    }

    /// <summary>The slot of the other player in front of the camera (any state), or 255.</summary>
    public static int Looked() => DownedAim.Looked()?.Slot ?? 255;

    /// <summary>Revive the player in <paramref name="slot"/> from here, as if fed.</summary>
    public static string Revive(int slot, float health = Downed.ReviveHealth)
    {
        Downed.Revive((byte)slot, health);
        return Status();
    }

    /// <summary>Spawn an item and put it in a hand of this game's player.</summary>
    public static string Give(string item, int hand = 0)
    {
        var player = HouseManager._?.player;
        if (player == null || !PickupBank.pickupsDict.TryGetValue(item, out var spawnable) || !player.TryGetComponent(out Player_Abilities rig))
            return "no player or no such item";
        var go = Object.Instantiate(spawnable.obj, player.transform.position + player.transform.forward * 0.5f, Quaternion.identity);
        rig.Pickup((Hands)hand, go);
        return rig.pickups[hand] == go ? "holding " + go.name : "couldn't hold " + go.name;
    }

    /// <summary>Fill the cup in a hand (0 dirt, 1 blood, 3 water, 4 cleaner).</summary>
    public static string Fill(int hand, int liquid)
    {
        var player = HouseManager._?.player;
        if (player == null || !player.TryGetComponent(out Player_Abilities rig) || rig.pickups[hand] == null
            || !rig.pickups[hand].TryGetComponent(out LiquidContainer cup))
            return "no cup held";
        cup.Fill(liquid);
        return $"filled with {liquid}: {cup.currentDrinkVolume} of {cup.maxDrinkVolume}, sip {cup.sipVolume}";
    }

    /// <summary>What the use button does with what's in a hand (the game's own call).</summary>
    public static string UseHeld(int hand = 0)
    {
        var player = HouseManager._?.player;
        if (player == null || !player.TryGetComponent(out Player_Abilities rig) || rig.pickups[hand] == null)
            return "nothing held";
        string name = rig.pickups[hand].name;
        rig.Use((Hands)hand);
        return "used " + name;
    }

    /// <summary>Stand 1.5 m from the other player's body, looking at it.</summary>
    public static string Face(int slot, float angle = 0f)
    {
        var puppet = RemotePlayers.Find((byte)slot);
        var player = HouseManager._?.player;
        if (puppet == null || player == null)
            return "no body or player";
        var target = puppet.transform.position;
        var from = target + Quaternion.Euler(0f, angle, 0f) * new Vector3(1.2f, 0.6f, 0.9f);
        player.transform.position = from;
        var flat = new Vector3(target.x - from.x, 0f, target.z - from.z);
        player.transform.rotation = Quaternion.LookRotation(flat);
        var camera = HouseManager._.mainCamera.transform;
        camera.rotation = Quaternion.LookRotation(target - camera.position);
        // The camera control keeps its own pitch and rewrites the rotation every frame.
        if (camera.TryGetComponent(out CameraControl control))
            Pitch(control) = camera.localEulerAngles.x > 180f ? camera.localEulerAngles.x - 360f : camera.localEulerAngles.x;
        return $"at {from}, looking at {Looked()}";
    }

    /// <summary>Hold or let go of a hand button (0 left, 1 right) through the input system.</summary>
    public static string Hand(int hand, bool pressed)
    {
        var mouse = Mouse.current;
        if (mouse == null)
            return "no mouse";
        using (StateEvent.From(mouse, out var eventPtr))
        {
            var button = hand == 0 ? mouse.leftButton : mouse.rightButton;
            button.WriteValueIntoEvent(pressed ? 1f : 0f, eventPtr);
            InputSystem.QueueEvent(eventPtr);
        }
        return $"hand {hand} {(pressed ? "down" : "up")}";
    }

    /// <summary>The fire extinguisher's explosion, centred on the other player's body (or this player when slot is 255).</summary>
    public static string Explode(int slot)
    {
        var centre = slot == 255 ? HouseManager._.player.transform.position : RemotePlayers.Find((byte)slot)?.transform.position;
        if (centre == null)
            return "no body";
        Commons.ExplosionDamage(centre.Value, 3f, 40f, EffectType.Impact,
            LayerMask.GetMask("Default", "Usable", "Player", "NotPlayer", "Construction", "Fire", "NotFire"), 1.2f, canImpact: true);
        return Status();
    }

    private static Vector3 _movedFrom;

    /// <summary>
    /// Put this game's first kill plane (or teleporter, switched on) where the other player's body is, so
    /// only this game sees it touch the body. <see cref="PutHazardBack"/> undoes it.
    /// </summary>
    public static string MoveHazardTo(int slot, bool teleporter)
    {
        var puppet = RemotePlayers.Find((byte)slot);
        if (puppet == null)
            return "no body";
        var hazard = teleporter
            ? Object.FindObjectsOfType<Teleporter>(true)[0].transform
            : Object.FindObjectsOfType<KillPlane>()[0].transform;
        _movedFrom = hazard.position;
        hazard.gameObject.SetActive(true);
        hazard.position = puppet.transform.position;
        return $"{hazard.name} moved from {_movedFrom} to {hazard.position}; drop-off {HouseManager._.packageDropOffPoint.position}";
    }

    public static string PutHazardBack(bool teleporter)
    {
        var hazard = teleporter
            ? Object.FindObjectsOfType<Teleporter>(true)[0].transform
            : Object.FindObjectsOfType<KillPlane>()[0].transform;
        hazard.position = _movedFrom;
        if (teleporter)
            hazard.gameObject.SetActive(false);
        return "back at " + _movedFrom;
    }

    private static readonly HarmonyLib.AccessTools.FieldRef<CameraControl, float> Pitch =
        HarmonyLib.AccessTools.FieldRefAccess<CameraControl, float>("yRotate");

    private static bool Dead() =>
        HouseManager._?.player != null && HouseManager._.player.TryGetComponent(out Player_Abilities rig) && rig.dead;
}
