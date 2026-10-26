using System;
using System.Collections.Generic;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DevBridge.Commands;

/// <summary>Textures in memory: how many, how big, and who uses one. Made for tracking down texture leaks.</summary>
internal static class TextureCommands
{
    [BridgeCommand("textures.count", "Every Texture2D and RenderTexture in memory, grouped by name and size, with a size estimate.",
        Params = new[] { "top?: groups to list, default 25" })]
    private static object Count(JObject p)
    {
        var textures = Resources.FindObjectsOfTypeAll<Texture2D>().Cast<Texture>()
            .Concat(Resources.FindObjectsOfTypeAll<RenderTexture>()).ToList();
        var groups = textures
            .GroupBy(t => $"{t.GetType().Name} '{t.name}' {t.width}x{t.height}")
            .Select(g => (key: g.Key, count: g.Count(), bytes: g.Sum(EstimateBytes)))
            .OrderByDescending(g => g.bytes)
            .ToList();

        return new JObject
        {
            ["texture2D"] = textures.Count(t => t is Texture2D),
            ["renderTextures"] = textures.Count(t => t is RenderTexture),
            ["estimatedMB"] = Math.Round(groups.Sum(g => g.bytes) / 1048576.0, 1),
            ["unnamed"] = textures.Count(t => string.IsNullOrEmpty(t.name)),
            // A render texture that isn't created holds no GPU memory, only the object.
            ["renderTexturesCreated"] = textures.OfType<RenderTexture>().Count(t => t.IsCreated()),
            ["renderTexturesByFormat"] = JObject.FromObject(textures.OfType<RenderTexture>()
                .GroupBy(t => t.format.ToString()).ToDictionary(g => g.Key, g => g.Count())),
            ["top"] = new JArray(groups.Take(p.Int("top", 25))
                .Select(g => $"{g.count} x {g.key}: {g.bytes / 1048576.0:0.0} MB").ToArray()),
        };
    }

    [BridgeCommand("textures.users", "What shows a texture: renderer materials, UI images and cameras rendering into it.",
        Params = new[] { "name: texture name (exact, case-insensitive)" })]
    private static object Users(JObject p)
    {
        string name = p.Str("name");
        bool Matches(Texture t) => t != null && string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase);
        var users = new JArray();

        foreach (var renderer in Resources.FindObjectsOfTypeAll<Renderer>())
        {
            if (!renderer.gameObject.scene.IsValid())
                continue;
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null)
                    continue;
                foreach (string property in material.GetTexturePropertyNames())
                {
                    if (Matches(material.GetTexture(property)))
                        users.Add($"renderer {Path(renderer.transform)} material '{material.name}' {property}");
                }
            }
        }
        foreach (var image in Resources.FindObjectsOfTypeAll<RawImage>())
        {
            if (image.gameObject.scene.IsValid() && Matches(image.texture))
                users.Add($"raw image {Path(image.transform)} (active: {image.isActiveAndEnabled})");
        }
        foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
        {
            if (camera.gameObject.scene.IsValid() && Matches(camera.targetTexture))
                users.Add($"camera {Path(camera.transform)} renders into it (active: {camera.isActiveAndEnabled})");
        }
        return users;
    }

    private static long EstimateBytes(Texture texture)
    {
        long pixels = (long)texture.width * texture.height;
        switch (texture)
        {
            case RenderTexture rt:
                int colour = rt.format == RenderTextureFormat.ARGBFloat ? 16 : rt.format == RenderTextureFormat.ARGBHalf ? 8 : 4;
                int depth = rt.depth >= 24 ? 4 : rt.depth > 0 ? 2 : 0;
                return pixels * (colour + depth) * Math.Max(1, rt.antiAliasing);
            case Texture2D t2d:
                long bytes = pixels * BytesPerPixel(t2d.format);
                return t2d.mipmapCount > 1 ? bytes * 4 / 3 : bytes;
            default:
                return pixels * 4;
        }
    }

    // Close enough for spotting leaks; compressed formats count as one byte per pixel.
    private static int BytesPerPixel(TextureFormat format)
    {
        switch (format)
        {
            case TextureFormat.RGBAFloat: return 16;
            case TextureFormat.RGBAHalf: return 8;
            case TextureFormat.RGB24: return 3;
            case TextureFormat.Alpha8:
            case TextureFormat.R8:
            case TextureFormat.DXT5:
            case TextureFormat.BC7:
                return 1;
            case TextureFormat.DXT1: return 1;
            default: return 4;
        }
    }

    private static string Path(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent)
            parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }
}
