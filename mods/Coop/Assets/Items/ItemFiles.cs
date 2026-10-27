using System;
using Coop.Assets.Transfer;
using Coop.Session;
using HarmonyLib;
using UnityEngine;

namespace Coop.Assets.Items;

/// <summary>
/// Puts a shared file on something in the house the way the game does it: the custom shop's picture
/// and tape loaders, the photo's loadImage, the computer's wallpaper picker. Their hooks
/// (<see cref="Hooks.LoaderPatches"/>) turn the shared path into this game's copy, or wait for it.
/// </summary>
internal static class ItemFiles
{
    /// <summary>CustomTextureSaveLoader.CustomizeItem(GameObject, string), which is private.</summary>
    public static readonly Action<CustomTextureSaveLoader, GameObject, string> LoadPicture =
        AccessTools.MethodDelegate<Action<CustomTextureSaveLoader, GameObject, string>>(
            AccessTools.Method(typeof(CustomTextureSaveLoader), "CustomizeItem", new[] { typeof(GameObject), typeof(string) }));

    public static void Apply(GameObject item, byte kind, string path)
    {
        switch (kind)
        {
            case ItemPaths.Texture:
                var picture = item.GetComponentInChildren<CustomTextureSaveLoader>(true);
                if (picture == null || SharedFiles.SameFile(picture.persistentTexturePath, path))
                    return;
                picture.persistentTexturePath = path;
                LoadPicture(picture, picture.gameObject, path);
                break;
            case ItemPaths.Music:
                var tape = item.GetComponentInChildren<CustomMusicSaveLoader>(true);
                if (tape == null || SharedFiles.SameFile(tape.persistentMusicPath, path))
                    return;
                tape.persistentMusicPath = path;
                if (tape.cassette == null)
                    tape.cassette = tape.GetComponent<Cassette>();
                tape.CustomizeItem(item, path);
                break;
            case ItemPaths.Photo:
                var photo = item.GetComponentInChildren<Picture>(true);
                // With Unofficial NEET Patches the photo's spawn already carried it.
                if (photo == null || SharedFiles.SameFile(photo.storedImagePath, path))
                    return;
                photo.storedImagePath = path;
                StartOn(photo, photo.loadImage(path, fadeIn: true));
                break;
        }
    }

    public static void ApplyWallpaper(string path)
    {
        var computer = HouseManager._ != null ? HouseManager._.mainComputer : null;
        var picker = computer != null ? computer.GetComponentInChildren<WallpaperPicker>(true) : null;
        if (picker == null || SharedFiles.SameFile(computer.wallpaperImgPath, path))
            return;
        computer.wallpaperImgPath = path;
        StartOn(picker, picker.LoadWallpaper(path));
    }

    /// <summary>Host: whoever sent a shared path has its file.</summary>
    public static void OfferedBy(string path, byte slot)
    {
        if (SharedPaths.TryParse(path, out string hash, out string ext))
            Hub.Offered(AssetId.OfPath(hash, ext), slot, "a file from " + (CoopSession.FindSlot(slot)?.Name ?? "a player"));
    }

    /// <summary>On <paramref name="owner"/>, or on the mod's own object while it's switched off (a computer screen that's off).</summary>
    public static void StartOn(MonoBehaviour owner, System.Collections.IEnumerator routine)
    {
        if (owner != null && owner.isActiveAndEnabled)
            owner.StartCoroutine(routine);
        else if (CoopMod.Instance != null)
            CoopMod.Instance.StartCoroutine(routine);
    }
}
