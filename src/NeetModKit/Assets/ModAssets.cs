using System;
using System.IO;
using NeetLoader;
using NeetLoader.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NeetModKit.Assets;

/// <summary>Loads images and asset bundles from a mod's folder. Loaded assets are freed when the mod unloads.</summary>
public static class ModAssets
{
    /// <summary>Loads a PNG or JPG. Relative paths are inside the mod folder.</summary>
    public static Texture2D LoadTexture(NeetMod owner, string path)
    {
        string full = Resolve(owner, path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true) { name = Path.GetFileNameWithoutExtension(full) };
        if (!tex.LoadImage(File.ReadAllBytes(full)))
        {
            Object.Destroy(tex);
            throw new InvalidDataException($"{full} isn't a PNG or JPG.");
        }
        owner.Track(Disposable.Destroying(tex));
        return tex;
    }

    public static Sprite LoadSprite(NeetMod owner, string path) => ToSprite(owner, LoadTexture(owner, path));

    public static Sprite ToSprite(NeetMod owner, Texture2D texture)
    {
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = texture.name;
        owner.Track(Disposable.Destroying(sprite));
        return sprite;
    }

    /// <summary>
    /// Loads a Unity AssetBundle (build it with Unity 2022.3 for Windows). Use this for models and
    /// prefabs; scripts in bundles must come from an assembly the mod already loaded.
    /// </summary>
    public static AssetBundle LoadBundle(NeetMod owner, string path)
    {
        var bundle = AssetBundle.LoadFromFile(Resolve(owner, path))
            ?? throw new InvalidDataException($"{path} isn't a valid asset bundle.");
        owner.Track(Disposable.Create(() => bundle.Unload(true)));
        return bundle;
    }

    /// <summary>
    /// Makes a readable copy of any texture, even ones the game marked unreadable, by drawing it
    /// through a render texture. Useful for recolouring the game's own art.
    /// </summary>
    public static Texture2D ReadableCopy(NeetMod owner, Texture source)
    {
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) { name = source.name + " (copy)" };
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            owner.Track(Disposable.Destroying(copy));
            return copy;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private static string Resolve(NeetMod owner, string path)
    {
        string full = Path.IsPathRooted(path) ? path : owner.PathTo(path);
        if (!File.Exists(full))
            throw new FileNotFoundException($"No file at {full}");
        return full;
    }
}
