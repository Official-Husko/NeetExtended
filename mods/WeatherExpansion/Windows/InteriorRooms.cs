using System.Collections.Generic;
using UnityEngine;

namespace WeatherExpansion.Windows;

/// <summary>
/// Which points are inside the house, from the trigger boxes of the rooms that aren't yards. A window
/// uses it to tell which of its two sides is the room (the panes' names don't say for every window).
/// </summary>
internal sealed class InteriorRooms
{
    private readonly List<Bounds> _boxes = new List<Bounds>();

    public InteriorRooms()
    {
        var outdoors = Commons.ExteriorLocations;
        foreach (var pair in Locator.rooms)
        {
            if (pair.Value == null || outdoors.Contains(pair.Key))
                continue;
            foreach (var collider in pair.Value.GetComponents<Collider>())
                _boxes.Add(collider.bounds);
        }
    }

    public bool Contains(Vector3 point)
    {
        for (int i = 0; i < _boxes.Count; i++)
        {
            if (_boxes[i].Contains(point))
                return true;
        }
        return false;
    }
}
