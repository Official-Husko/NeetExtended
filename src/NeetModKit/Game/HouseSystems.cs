using System;
using UnityEngine;

namespace NeetModKit.Game;

/// <summary>Fire and power, per room or for the whole house.</summary>
public static class HouseSystems
{
    public static bool IsOnFire(Location room) => Locator.GetRoom(room)?.fireSpreader != null;

    public static void StartFire(Location room)
    {
        var center = Rooms.Center(room) ?? throw new ArgumentException($"{room} isn't in this scene.");
        // Starting exactly at the centre gives the spreader a zero-length first ray, so lift it a bit.
        Locator.StartRoomFire(room, center + Vector3.up * 0.3f);
    }

    public static void StopFire(Location room)
    {
        var spreader = Locator.GetRoom(room)?.fireSpreader;
        if (spreader != null)
            spreader.GetComponent<FireSpreader>().Clear();
    }

    /// <summary>Forces a room's power on or off, ignoring the fuses.</summary>
    public static void SetPower(Location room, bool on) => FuseBox().UpdateRoomPowerState(room, on);

    /// <summary>Forces power for the whole house, ignoring the fuses.</summary>
    public static void SetHousePower(bool on) => FuseBox().UpdateHousePowerState(on);

    private static FuseBox_Manager FuseBox() =>
        GameRefs.House?.fuseBox_Manager ?? throw new InvalidOperationException("No fuse box (not in the house?).");
}
