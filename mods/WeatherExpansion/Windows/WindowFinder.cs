using System.Collections.Generic;
using UnityEngine;

namespace WeatherExpansion.Windows;

/// <summary>Finds the house's window panes once per house load (never per frame).</summary>
internal static class WindowFinder
{
    private const string Prefix = "Wall_Window";

    public static List<WindowPane> Find()
    {
        var found = new List<WindowPane>();
        var seen = new HashSet<Transform>();
        var rooms = new InteriorRooms();
        foreach (var breakable in Object.FindObjectsOfType<BreakOnImpact>())
        {
            var pane = breakable.transform;
            var window = pane.parent;
            if (window == null || !window.name.StartsWith(Prefix) || !pane.name.Contains("_Pane_") || !seen.Add(window))
                continue;
            Transform first = null, second = null;
            foreach (Transform child in window)
            {
                if (child.name.EndsWith("_Pane_1")) first = child;
                else if (child.name.EndsWith("_Pane_2")) second = child;
            }
            if (first != null && second != null && first.GetComponent<Renderer>() != null && second.GetComponent<Renderer>() != null)
                found.Add(new WindowPane(window, first, second, rooms));
        }
        return found;
    }
}
