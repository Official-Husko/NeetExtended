using System.Collections.Generic;
using PerformanceFixes.Common;
using UnityEngine;

namespace PerformanceFixes.Rendering;

/// <summary>
/// The front door has a camera ("PeepholeCamera") that films the porch into PeepholeRT for the
/// picture in the door's peephole, every frame, from anywhere in the house. It renders the whole
/// scene with shadows and post-processing: 1.1 ms and 500 draw calls a frame in the living room.
/// This keeps it on only while the player is close enough to see the peephole. The other doors'
/// peephole cameras are already switched on and off by Door.OnTriggerEnter/Exit and are left alone.
/// </summary>
internal static class Peepholes
{
    private const string PeepholeTexture = "PeepholeRT";
    private const float Range = 6f;
    private const float CheckEvery = 0.2f;

    private static readonly List<Camera> Cameras = new List<Camera>();
    private static float _nextCheck;

    public static int Count => Cameras.Count;

    /// <summary>Finds always-on peephole cameras. Run when the house is ready.</summary>
    public static void Find()
    {
        Restore();
        var doorCameras = new HashSet<Camera>(ByReference<Camera>.Instance);
        foreach (var door in Object.FindObjectsOfType<Door>())
        {
            if (door.keyHoleRTCams != null)
                doorCameras.UnionWith(door.keyHoleRTCams);
        }
        foreach (var camera in Object.FindObjectsOfType<Camera>())
        {
            var target = camera.targetTexture;
            if (target != null && target.name == PeepholeTexture && !doorCameras.Contains(camera))
                Cameras.Add(camera);
        }
        _nextCheck = 0f;
    }

    public static void Update()
    {
        if (Cameras.Count == 0 || Time.unscaledTime < _nextCheck)
            return;
        _nextCheck = Time.unscaledTime + CheckEvery;

        var house = HouseManager._;
        if (house == null || house.player == null)
            return;
        var player = house.player.transform.position;
        for (int i = 0; i < Cameras.Count; i++)
        {
            var camera = Cameras[i];
            if (camera == null)
                continue;
            bool near = (camera.transform.position - player).sqrMagnitude < Range * Range;
            if (camera.enabled != near)
                camera.enabled = near;
        }
    }

    /// <summary>Switches the cameras back on, as the game has them.</summary>
    public static void Restore()
    {
        foreach (var camera in Cameras)
        {
            if (camera != null)
                camera.enabled = true;
        }
        Cameras.Clear();
    }
}
