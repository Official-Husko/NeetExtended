using System.Collections.Generic;
using PerformanceFixes.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PerformanceFixes.Leaks;

/// <summary>
/// Textures and materials the game creates for scene objects and never destroys. They're
/// registered with the scene of the object that uses them and destroyed when that scene unloads.
/// Nothing else frees them: the game's paint textures from every earlier house load were still
/// in memory hours later, even after Resources.UnloadUnusedAssets.
/// </summary>
internal static class SceneOwned
{
    private static readonly Dictionary<int, HashSet<Object>> ByScene = new Dictionary<int, HashSet<Object>>();

    public static int Count
    {
        get
        {
            int count = 0;
            foreach (var set in ByScene.Values)
                count += set.Count;
            return count;
        }
    }

    public static void Start() => SceneManager.sceneUnloaded += OnSceneUnloaded;

    public static void Stop()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        // What's registered still belongs to live objects; leave it to them.
        ByScene.Clear();
    }

    public static void Own(Scene scene, Object thing)
    {
        if (thing == null || !scene.IsValid())
            return;
        if (!ByScene.TryGetValue(scene.handle, out var owned))
            ByScene[scene.handle] = owned = new HashSet<Object>(ByReference<Object>.Instance);
        owned.Add(thing);
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        if (!ByScene.TryGetValue(scene.handle, out var owned))
            return;
        ByScene.Remove(scene.handle);
        int destroyed = 0;
        foreach (var thing in owned)
        {
            if (thing == null)
                continue;
            Object.Destroy(thing);
            destroyed++;
        }
        PerformanceFixesMod.Instance?.Log.Debug($"Freed {destroyed} textures and materials left by {scene.name}.");
    }
}
