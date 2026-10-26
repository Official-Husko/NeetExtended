using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeetModKit.Game;

/// <summary>Room (<see cref="Location"/>) lookups that don't throw on bad input.</summary>
public static class Rooms
{
    public static IEnumerable<Location> All => (Location[])Enum.GetValues(typeof(Location));

    public static bool TryParse(string name, out Location location) =>
        Enum.TryParse(name, ignoreCase: true, out location) && Enum.IsDefined(typeof(Location), location);

    public static Location Parse(string name) =>
        TryParse(name, out var location) ? location : throw new ArgumentException($"Unknown room '{name}'. Try one of: {string.Join(", ", All)}");

    /// <summary>Whether the room exists in the current scene.</summary>
    public static bool Exists(Location location) =>
        Locator.rooms != null && Locator.rooms.TryGetValue(location, out var go) && go != null;

    public static Vector3? Center(Location location) =>
        Exists(location) ? Locator.GetRoomCenter(location) : (Vector3?)null;

    /// <summary>A point on the floor near the room centre, nudged up so things don't spawn inside the floor.</summary>
    public static Vector3? FloorPoint(Location location, float lift = PlayerControl.StandingHeight)
    {
        var center = Center(location);
        if (center == null)
            return null;
        return Locator.GetFlooredPositionInRoom(location, center.Value) + Vector3.up * lift;
    }

    public static Location At(Vector3 point) => Locator.LocatePoint(point);
}
