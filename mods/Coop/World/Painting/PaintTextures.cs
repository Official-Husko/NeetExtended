using System.Collections.Generic;
using UnityEngine;

namespace Coop.World.Painting;

/// <summary>
/// Splat textures travel by name: every game has the same assets. Names are cached both ways, since
/// reading <c>Object.name</c> allocates and finding a texture by name means walking every loaded one.
/// </summary>
internal static class PaintTextures
{
    private const float RescanAfter = 5f;

    private static readonly Dictionary<Texture2D, string> Names = new Dictionary<Texture2D, string>();
    private static readonly Dictionary<string, Texture2D> ByName = new Dictionary<string, Texture2D>();
    private static readonly HashSet<string> Missing = new HashSet<string>();
    private static float _scanned = float.NegativeInfinity;

    /// <summary>Unknown names asked for so far, for the probes.</summary>
    public static int MissingCount => Missing.Count;

    /// <summary>The texture's name; "" for none (some brushes have no texture).</summary>
    public static string NameOf(Texture2D texture)
    {
        if (texture == null)
            return "";
        if (!Names.TryGetValue(texture, out string name))
            Names[texture] = name = texture.name;
        return name;
    }

    /// <summary>The loaded texture with this name, or null (also for "").</summary>
    public static Texture2D Find(string name)
    {
        if (name.Length == 0)
            return null;
        if (ByName.TryGetValue(name, out var texture) && texture != null)
            return texture;
        if (Time.realtimeSinceStartup - _scanned < RescanAfter)
            return null;
        Scan();
        if (ByName.TryGetValue(name, out texture) && texture != null)
            return texture;
        if (Missing.Add(name))
            CoopMod.Log.Warn($"Paint: no texture called \"{name}\" here; splats with it are drawn without one.");
        return null;
    }

    public static void Clear()
    {
        Names.Clear();
        ByName.Clear();
        Missing.Clear();
        _scanned = float.NegativeInfinity;
    }

    // Brush textures first, so a name that two textures share means the one the painters use.
    private static void Scan()
    {
        _scanned = Time.realtimeSinceStartup;
        ByName.Clear();
        foreach (var p in Resources.FindObjectsOfTypeAll<CollisionPainter>())
            Add(p.brush);
        foreach (var p in Resources.FindObjectsOfTypeAll<ParticlePainter>())
            Add(p.brush);
        foreach (var p in Resources.FindObjectsOfTypeAll<FootprintPainter>())
            Add(p.brush);
        foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            string name = texture.name;
            if (name.Length > 0 && !ByName.ContainsKey(name))
                ByName[name] = texture;
        }
    }

    private static void Add(Brush brush)
    {
        var texture = brush?.splatTexture;
        if (texture == null)
            return;
        string name = texture.name;
        if (!ByName.ContainsKey(name))
            ByName[name] = texture;
    }
}
