using System.Collections.Generic;
using System.Text;
using NeetModKit.Game;
using UnityEngine;

namespace Coop.World.Painting;

/// <summary>
/// Looks at the house's paint surfaces from the dev bridge (they read textures back from the GPU, so
/// they're for tests only): <c>neet call type.call type=Coop.World.Painting.PaintProbes method=Coverage args=[382]</c>.
/// </summary>
public static class PaintProbes
{
    /// <summary>The house's paint surfaces: how many, their id range, how far they reach, which can move.</summary>
    public static string Targets()
    {
        var house = HouseManager._;
        if (house == null || house.paintTargets == null)
            return "No house.";
        int count = 0, nulls = 0, moving = 0, minId = int.MaxValue, maxId = int.MinValue;
        var ids = new HashSet<int>();
        int duplicates = 0;
        var min = Vector3.positiveInfinity;
        var max = Vector3.negativeInfinity;
        float farthest = 0f;
        var movers = new StringBuilder();
        foreach (var target in house.paintTargets)
        {
            if (target == null)
            {
                nulls++;
                continue;
            }
            count++;
            if (!ids.Add(target.saveId))
                duplicates++;
            minId = Mathf.Min(minId, target.saveId);
            maxId = Mathf.Max(maxId, target.saveId);
            if (target.TryGetComponent(out Renderer renderer))
            {
                var b = renderer.bounds;
                min = Vector3.Min(min, b.min);
                max = Vector3.Max(max, b.max);
                var p = target.transform.position;
                farthest = Mathf.Max(farthest, (b.min - p).magnitude, (b.max - p).magnitude);
            }
            var body = target.GetComponentInParent<Rigidbody>();
            if (body != null)
            {
                moving++;
                if (movers.Length < 600)
                    movers.Append(target.name).Append(body.isKinematic ? " (kinematic)" : "").Append(", ");
            }
        }
        int inScene = 0;
        var outside = new StringBuilder();
        foreach (var target in Object.FindObjectsOfType<PaintTarget>(true))
        {
            inScene++;
            if (!Surfaces.IsShared(target) && outside.Length < 300)
                outside.Append(target.name).Append(", ");
        }
        return $"{count} surfaces ({nulls} null, {duplicates} duplicate ids), ids {minId}..{maxId}, {inScene} PaintTargets in the scene, not the house's: {outside}. " +
            $"Bounds {min} .. {max}, farthest corner from its own pivot {farthest:0.0} m. {moving} under a rigidbody: {movers}";
    }

    /// <summary>Splat textures the house's painters use, by name.</summary>
    public static string Textures()
    {
        var names = new SortedDictionary<string, int>();
        void Add(Brush brush)
        {
            if (brush == null)
                return;
            string name = brush.splatTexture != null ? brush.splatTexture.name : "(none)";
            names.TryGetValue(name, out int n);
            names[name] = n + 1;
        }
        foreach (var p in Resources.FindObjectsOfTypeAll<CollisionPainter>())
            Add(p.brush);
        foreach (var p in Resources.FindObjectsOfTypeAll<ParticlePainter>())
            Add(p.brush);
        foreach (var p in Resources.FindObjectsOfTypeAll<FootprintPainter>())
            Add(p.brush);
        var text = new StringBuilder();
        foreach (var pair in names)
            text.Append(pair.Key).Append(" x").Append(pair.Value).Append(", ");
        var all = Resources.FindObjectsOfTypeAll<Texture2D>();
        var counts = new Dictionary<string, int>();
        foreach (var t in all)
        {
            counts.TryGetValue(t.name, out int n);
            counts[t.name] = n + 1;
        }
        text.Append("| same name twice: ");
        foreach (var pair in names)
        {
            if (counts.TryGetValue(pair.Key, out int n) && n > 1)
                text.Append(pair.Key).Append(" x").Append(n).Append(", ");
        }
        text.Append($"| {all.Length} textures loaded");
        return text.ToString();
    }

    /// <summary>
    /// How much of a surface is painted: pixels over half in each channel (dirt, blood, burn, water).
    /// Reads the texture back from the GPU, so only for tests.
    /// </summary>
    public static string Coverage(int saveId)
    {
        var target = Surfaces.Find(saveId);
        if (target == null)
            return $"No surface {saveId}.";
        if (!target.setupComplete)
            return "0 0 0 0 (never painted)";
        var texture = PaintSurfaces.Latest(target);
        var copy = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false);
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        copy.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
        RenderTexture.active = previous;
        var pixels = copy.GetPixels32();
        Object.Destroy(copy);
        int r = 0, g = 0, b = 0, a = 0;
        long sum = 0;
        foreach (var p in pixels)
        {
            if (p.r > 127) r++;
            if (p.g > 127) g++;
            if (p.b > 127) b++;
            if (p.a > 127) a++;
            sum += p.r + p.g + p.b + p.a;
        }
        return $"{r} {g} {b} {a} (sum {sum})";
    }

    /// <summary>A surface's paint state: set up, valid, switched on, splats waiting, which texture is current.</summary>
    public static string Info(int saveId)
    {
        var target = Surfaces.Find(saveId);
        if (target == null)
            return $"No surface {saveId}.";
        var t = HarmonyLib.Traverse.Create(target);
        var queue = t.Field("m_Splats").GetValue<List<global::Paint>>();
        return $"{target.name} at {target.transform.position}: setup {target.setupComplete}, valid {t.Field("validTarget").GetValue<bool>()}, " +
            $"enabled {target.enabled}, active {target.gameObject.activeInHierarchy}, waiting {queue?.Count}, evenFrame {t.Field("evenFrame").GetValue<bool>()}, " +
            $"size {target.paintTextureSize}, wet {target.surfaceWet}, shared {Surfaces.IsShared(target)}, " +
            $"material shows {Shown(target, t)}, positions {Sum(t.Field("worldPosTex").GetValue<RenderTexture>())}";
    }

    /// <summary>Surfaces set up for paint whose material doesn't show their paint texture (set up too early).</summary>
    public static string Unbound()
    {
        var house = HouseManager._;
        if (house == null)
            return "No house.";
        int setup = 0, unbound = 0, onStart = 0;
        var ids = new StringBuilder();
        foreach (var target in house.paintTargets)
        {
            if (target != null && target.SetupOnStart)
                onStart++;
            if (target == null || !target.setupComplete)
                continue;
            setup++;
            if (Shown(target, HarmonyLib.Traverse.Create(target)) != "nothing")
                continue;
            unbound++;
            if (ids.Length < 300)
                ids.Append(target.saveId).Append(' ');
        }
        return $"{setup} set up ({onStart} set up on start), {unbound} not showing their paint: {ids}";
    }

    private static string Shown(PaintTarget target, HarmonyLib.Traverse t)
    {
        var renderer = target.GetComponent<Renderer>();
        var shown = renderer != null ? renderer.sharedMaterial.GetTexture("_SplatTex") : null;
        if (shown == null)
            return "nothing";
        if (shown == target.splatTex)
            return "splatTex";
        return shown == t.Field("splatTexAlt").GetValue<RenderTexture>() ? "splatTexAlt" : "another texture " + shown.name;
    }

    private static string Sum(RenderTexture texture)
    {
        if (texture == null)
            return "none";
        var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false);
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        copy.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
        RenderTexture.active = previous;
        int set = 0;
        foreach (var p in copy.GetPixels())
        {
            if (p.a > 0f || p.r != 0f || p.g != 0f || p.b != 0f)
                set++;
        }
        Object.Destroy(copy);
        return set + " texels";
    }
}
