using System.Collections.Generic;
using NeetLoader.Runtime;
using UnityEngine;

namespace NeetModKit.Scenes;

/// <summary>
/// Reversible changes to scene objects. Hidden objects come back when the owner cleans up, and
/// several owners can hide the same object: it only shows again once all of them are done.
/// </summary>
public static class SceneEdits
{
    private static readonly Dictionary<GameObject, int> HiddenBy = new Dictionary<GameObject, int>();

    public static void Hide(ITracker owner, GameObject go)
    {
        if (go == null)
            return;
        HiddenBy.TryGetValue(go, out int count);
        if (count == 0 && !go.activeSelf)
            return; // Hidden by the game itself; leave it to the game.
        HiddenBy[go] = count + 1;
        go.SetActive(false);

        owner.Track(Disposable.Create(() =>
        {
            if (!HiddenBy.TryGetValue(go, out int left))
                return;
            if (left > 1)
            {
                HiddenBy[go] = left - 1;
                return;
            }
            HiddenBy.Remove(go);
            if (go != null)
                go.SetActive(true);
        }));
    }

    /// <summary>Scene root objects whose name starts with <paramref name="prefix"/> (e.g. "DEMO_BLOCKER_PANTRY").</summary>
    public static List<GameObject> RootsNamed(string prefix)
    {
        var found = new List<GameObject>();
        foreach (var root in SceneQuery.Roots())
        {
            if (root.name.StartsWith(prefix))
                found.Add(root);
        }
        return found;
    }
}
