using UnityEngine;

namespace Coop.World.Painting;

/// <summary>
/// Paint tests from the dev bridge: counters, tracing, merge settings and ways to paint like the game
/// does. <c>neet call type.call type=Coop.World.Painting.PaintTestApi method=Stats --instance 2</c>.
/// Surface probes are in <see cref="PaintProbes"/>.
/// </summary>
public static class PaintTestApi
{
    /// <summary>Switches the merging of repeated splats (on by default), to compare.</summary>
    public static string Merge(bool on)
    {
        PaintRepeats.Enabled = on;
        return "Merging repeats " + (on ? "on." : "off.");
    }

    /// <summary>Merge settings: window (s), smallest cell (m), cell per splat size, splats allowed per cell and window.</summary>
    public static string MergeTune(float window, float smallestCell, float cellPerSize, int allowed)
    {
        PaintRepeats.Window = window;
        PaintRepeats.SmallestCell = smallestCell;
        PaintRepeats.CellPerSize = cellPerSize;
        PaintRepeats.Allowed = allowed;
        PaintRepeats.Clear();
        return $"window {window}, cell {smallestCell} or {cellPerSize} of the size, {allowed} per cell.";
    }

    public static string Reset()
    {
        PaintStats.Clear();
        return "Counters reset.";
    }

    public static string Stats() => PaintStats.Report() + $"; missing textures {PaintTextures.MissingCount}";

    /// <summary>Counts which game code paints for the next few seconds; read it with <see cref="Callers"/>.</summary>
    public static string Trace(float seconds = 10f)
    {
        PaintTrace.Begin(seconds);
        return $"Tracing painters for {seconds} s.";
    }

    public static string Callers() => PaintTrace.Report();

    /// <summary>
    /// Paints like a drop of blood landing straight below a point (channel 0 dirt, 1 blood, 2 burn,
    /// 3 water, 4 cleaner), the given number of times. Returns the surface's id.
    /// </summary>
    public static string Splat(float x, float y, float z, int channel = 1, float scale = 1f, int times = 1, float spin = 180f)
    {
        Water.SetupSpheres();
        var brush = new Brush();
        brush.CopyBrush(Water.liquidSpheresGrav[1].GetComponent<CollisionPainter>().brush);
        brush.splatChannel = channel;
        brush.splatScale = scale;
        brush.splatRandomRotation = spin;
        var ray = new Ray(new Vector3(x, y, z), Vector3.down);
        if (!Physics.Raycast(ray, out var hit, 20f, LayerMask.GetMask("Construction", "Default", "Usable")) || !hit.collider.TryGetComponent(out PaintTarget target))
            return "Nothing paintable below.";
        for (int i = 0; i < times; i++)
            PaintTarget.PaintObject(target, hit.point, hit.normal, brush);
        return $"Painted {times}x on {target.name} (surface {target.saveId}) at {hit.point}, texture {PaintTextures.NameOf(brush.splatTexture)} {brush.splatsX}x{brush.splatsY}.";
    }

    /// <summary>
    /// Drops the game's own liquid blobs at a point, as bleeding or a spill does (0 poop, 1 blood,
    /// 2 burn, 3 water, 4 cleaner). They paint where they land.
    /// </summary>
    public static string Drop(float x, float y, float z, int channel = 1, int count = 5)
    {
        Water.SetupSpheres();
        var prefab = Water.liquidSpheresGrav[Mathf.Clamp(channel, 0, 4)];
        for (int i = 0; i < count; i++)
            Object.Instantiate(prefab, new Vector3(x, y, z) + Random.insideUnitSphere * 0.2f, Quaternion.identity);
        var brush = prefab.GetComponent<CollisionPainter>()?.brush;
        return $"Dropped {count} {prefab.name}; brush scale {brush?.splatScale} random {brush?.splatRandomScaleMin}..{brush?.splatRandomScaleMax}, texture {PaintTextures.NameOf(brush?.splatTexture)} {brush?.splatsX}x{brush?.splatsY}.";
    }

    /// <summary>Dries a surface's water now, as the house does 10 to 15 minutes after the last splash.</summary>
    public static string Dry(int saveId)
    {
        var target = Surfaces.Find(saveId);
        if (target == null || !target.setupComplete)
            return $"No painted surface {saveId}.";
        target.ClearWaterChannel();
        return $"Dried {target.name}.";
    }

    /// <summary>Turns this game's player and camera to look at a point, for screenshots.</summary>
    public static string View(float x, float y, float z)
    {
        var house = HouseManager._;
        var player = WorldSide.LocalPlayer;
        if (house == null || player == null || house.mainCamera == null)
            return "No player.";
        var camera = house.mainCamera.transform;
        var to = new Vector3(x, y, z) - camera.position;
        player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0f);
        float pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
        if (camera.TryGetComponent(out CameraControl control))
            HarmonyLib.Traverse.Create(control).Field("yRotate").SetValue(pitch);
        camera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        return $"Looking at ({x}, {y}, {z}), pitch {pitch:0}.";
    }

    /// <summary><see cref="Splat"/> under this game's player.</summary>
    public static string SplatHere(int channel = 1, float scale = 1f, int times = 1)
    {
        var player = WorldSide.LocalPlayer;
        if (player == null)
            return "No player.";
        var p = player.transform.position + Vector3.up * 0.5f;
        return Splat(p.x, p.y, p.z, channel, scale, times);
    }
}
