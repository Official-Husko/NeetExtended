using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NeetModKit.Game;

/// <summary>Reading and changing the player's needs, position and survival.</summary>
public static class PlayerControl
{
    private static readonly AccessTools.FieldRef<CameraControl, float> CameraPitch =
        AccessTools.FieldRefAccess<CameraControl, float>("yRotate");

    /// <summary>How far above the floor the player's position sits when standing.</summary>
    public const float StandingHeight = 0.91f;

    private static readonly AccessTools.FieldRef<Stat, float> BaseValue = AccessTools.FieldRefAccess<Stat, float>("initValue");

    /// <summary>When on, every death path is blocked and needs stop draining.</summary>
    public static bool GodMode { get; private set; }

    /// <summary>Needs (hunger, bladder, hygiene, energy) stop draining. Also implied by god mode.</summary>
    public static bool NeedsFrozen { get; private set; }

    /// <summary>Walking speed multiplier (1 is normal).</summary>
    public static float SpeedMultiplier { get; private set; } = 1f;

    public static PlayerStats Stats =>
        GameRefs.PlayerStats ?? throw new InvalidOperationException("No player right now (not in the house?).");

    /// <summary>Current value of every stat, keyed by its short name (Health, Hunger, ...).</summary>
    public static Dictionary<string, float> ReadStats()
    {
        var stats = Stats;
        var result = new Dictionary<string, float>(stats.statTypes.Count);
        foreach (var type in stats.statTypes)
            result[type.Name] = stats.stats[type].GetVal();
        return result;
    }

    public static Stat FindStat(string name)
    {
        var stats = Stats;
        foreach (var type in stats.statTypes)
        {
            if (string.Equals(type.Name, name, StringComparison.OrdinalIgnoreCase))
                return stats.stats[type];
        }
        throw new ArgumentException($"Unknown stat '{name}'. Known: {string.Join(", ", ReadStats().Keys)}");
    }

    /// <summary>Sets a stat through the game's setter, so thresholds and UI react as normal.</summary>
    public static void SetStat(string name, float value) => FindStat(name).SetVal(value);

    public static void SetGodMode(bool enabled)
    {
        GodMode = enabled;
        ApplyFlags();
    }

    public static void SetNeedsFrozen(bool frozen)
    {
        NeedsFrozen = frozen;
        ApplyFlags();
    }

    /// <summary>
    /// Speed is a "sticky" stat the game resets to its base value every frame, so the base value
    /// is what has to change.
    /// </summary>
    public static void SetSpeedMultiplier(float multiplier)
    {
        SpeedMultiplier = Mathf.Clamp(multiplier, 0.1f, 10f);
        ApplyFlags();
    }

    /// <summary>Applies the switches to the current player object (it's replaced on respawn and house load).</summary>
    internal static void ApplyFlags()
    {
        var stats = GameRefs.PlayerStats;
        if (stats == null)
            return;
        stats.invincible = GodMode;
        stats.pausedNeeds = GodMode || NeedsFrozen;
        if (stats.speed != null)
            BaseValue(stats.speed) = SpeedMultiplier;
    }

    /// <summary>Moves the player (and whatever they're holding) to a point, killing momentum.</summary>
    public static void Teleport(Vector3 target)
    {
        var player = GameRefs.Player ?? throw new InvalidOperationException("No player right now.");
        var abilities = GameRefs.PlayerAbilities;
        var body = player.GetComponent<Rigidbody>();

        Vector3 delta = target - player.transform.position;
        if (body != null)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = target;
        }
        player.transform.position = target;

        if (abilities == null)
            return;
        foreach (var held in abilities.pickups)
        {
            if (held != null)
                held.transform.position += delta;
        }
    }

    /// <summary>Turns the player's body (yaw) and camera (pitch) to look along a direction.</summary>
    public static void Look(Vector3 direction)
    {
        var player = GameRefs.Player ?? throw new InvalidOperationException("No player right now.");
        var flat = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (flat.sqrMagnitude > 0.0001f)
            player.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);

        var camera = player.GetComponentInChildren<CameraControl>();
        if (camera != null)
        {
            // CameraControl pitches with positive values looking down.
            float pitch = -Mathf.Atan2(direction.y, flat.magnitude) * Mathf.Rad2Deg;
            CameraPitch(camera) = Mathf.Clamp(pitch, -89f, 89f);
        }
    }

    public static void Teleport(Location room)
    {
        var point = Rooms.FloorPoint(room) ?? throw new ArgumentException($"{room} isn't in this scene.");
        Teleport(point);
    }
}
