using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Economy;

/// <summary>
/// What the custom shops do to an item when its box is opened: a picture (custom photo) or a song
/// (custom cassette) from a file. The game keeps that as a delegate on the box, which can't travel,
/// so it's described here as a kind and a file path and made again on the other side.
///
/// Delegates come from <c>CustomItemShop</c> itself, or from a closure around one (Unofficial NEET
/// Patches sets the cassette shop's buy button up again); which file the shop holds is the order's.
/// </summary>
internal sealed class BoxCustomization
{
    public const byte None = 0;
    public const byte Picture = 1;
    public const byte Song = 2;

    private static readonly Action<CustomTextureSaveLoader, GameObject, string> LoadPicture =
        AccessTools.MethodDelegate<Action<CustomTextureSaveLoader, GameObject, string>>(
            AccessTools.Method(typeof(CustomTextureSaveLoader), "CustomizeItem", new[] { typeof(GameObject), typeof(string) }));

    private static bool _toldUnknown;

    public readonly byte Kind;
    public readonly string Path;

    public BoxCustomization(byte kind, string path)
    {
        Kind = kind;
        Path = path ?? "";
    }

    public Action<GameObject> AsDelegate() => Apply;

    /// <summary>
    /// The game's own unboxing step, with the file read from this machine: missing files leave the
    /// path set (the item saves it, and games that have the file show it) and nothing loaded.
    /// </summary>
    public void Apply(GameObject content)
    {
        if (content == null)
            return;
        bool present = Path.Length > 0 && File.Exists(Path);
        if (Kind == Song)
        {
            var music = content.GetComponentInChildren<CustomMusicSaveLoader>();
            if (music == null)
                return;
            music.persistentMusicPath = Path;
            if (present)
                music.CustomizeItem(content);
        }
        else if (Kind == Picture)
        {
            var picture = content.GetComponentInChildren<CustomTextureSaveLoader>();
            if (picture == null)
                return;
            picture.persistentTexturePath = Path;
            if (present)
                LoadPicture(picture, picture.gameObject, Path);
        }
    }

    /// <summary>The custom shop order behind <paramref name="onUnbox"/>, or false when it isn't one.</summary>
    public static bool Describe(Action<GameObject> onUnbox, out byte kind, out string path)
    {
        kind = None;
        path = "";
        if (onUnbox == null)
            return false;
        if (onUnbox.Target is BoxCustomization own)
        {
            kind = own.Kind;
            path = own.Path;
            return own.Kind != None;
        }
        var shop = ShopOf(onUnbox.Target);
        if (shop == null)
        {
            if (!_toldUnknown)
            {
                _toldUnknown = true;
                CoopMod.Log.Info($"World: an order came with an unboxing step from {onUnbox.Method.DeclaringType?.FullName}.{onUnbox.Method.Name}, which other games can't repeat; the item arrives plain there.");
            }
            return false;
        }
        kind = shop.filterForMusic || onUnbox.Method.Name == "CustomizeMediaURLsOnItem" ? Song : Picture;
        path = shop.latestFilePath ?? "";
        return true;
    }

    private static CustomItemShop ShopOf(object target)
    {
        if (target is CustomItemShop shop)
            return shop;
        if (target == null)
            return null;
        // A closure (cold path, once per order): its captured shop.
        foreach (var field in target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (typeof(CustomItemShop).IsAssignableFrom(field.FieldType))
                return field.GetValue(target) as CustomItemShop;
        }
        return null;
    }
}
