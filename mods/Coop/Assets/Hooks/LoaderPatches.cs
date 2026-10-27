using System.Collections;
using Coop.Assets.Items;
using HarmonyLib;
using UnityEngine;

namespace Coop.Assets.Hooks;

/// <summary>
/// The game's own loaders for files on things in the house: the custom shop's picture and tape, the
/// photo, the computer's wallpaper. A shared path is swapped for this game's copy right before the
/// file is read, or the load waits until the file has arrived and runs again then. Plain paths go
/// through <see cref="LoadRules"/>. Always on: a save made in co-op keeps shared paths, which only
/// these hooks turn into this game's cache.
/// </summary>
[HarmonyPatch]
internal static class LoaderPatches
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(CustomTextureSaveLoader), "CustomizeItem", new[] { typeof(GameObject), typeof(string) })]
    private static bool CustomPicture(CustomTextureSaveLoader __instance, GameObject obj, ref string path)
    {
        switch (SharedFiles.Resolve(path, out string local))
        {
            case Resolved.Ready:
                // The item keeps this game's copy, so a save here works without co-op too.
                if (__instance.persistentTexturePath == path)
                    __instance.persistentTexturePath = local;
                path = local;
                return true;
            case Resolved.Pending:
                var loader = __instance;
                SharedFiles.WhenReady(path, "a custom picture", file =>
                {
                    if (loader != null && obj != null)
                        ItemFiles.LoadPicture(loader, obj, file);
                });
                return false;
        }
        if (LoadRules.FromOtherGame(path))
        {
            __instance.persistentTexturePath = "";
            return false;
        }
        return LoadRules.MayRead(path);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(CustomMusicSaveLoader), nameof(CustomMusicSaveLoader.CustomizeItem))]
    private static bool CustomTape(CustomMusicSaveLoader __instance, GameObject obj, ref string filePath)
    {
        string path = string.IsNullOrEmpty(filePath) ? __instance.persistentMusicPath : filePath;
        switch (SharedFiles.Resolve(path, out string local))
        {
            case Resolved.Ready:
                if (__instance.persistentMusicPath == path)
                    __instance.persistentMusicPath = local;
                filePath = local;
                return true;
            case Resolved.Pending:
                var loader = __instance;
                SharedFiles.WhenReady(path, "a custom tape", file =>
                {
                    if (loader != null)
                        loader.CustomizeItem(obj, file);
                });
                return false;
        }
        if (LoadRules.FromOtherGame(path))
        {
            __instance.persistentMusicPath = "";
            return false;
        }
        return LoadRules.MayRead(path);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(Picture), nameof(Picture.loadImage))]
    private static bool Photo(Picture __instance, ref string filePath, bool fadeIn, ref IEnumerator __result)
    {
        bool given = !string.IsNullOrEmpty(filePath);
        string path = given ? filePath : __instance.storedImagePath;
        switch (SharedFiles.Resolve(path, out string local))
        {
            case Resolved.Ready:
                filePath = local;
                return true;
            case Resolved.Pending:
                var picture = __instance;
                SharedFiles.WhenReady(path, "a photo", file =>
                {
                    if (picture != null)
                        ItemFiles.StartOn(picture, picture.loadImage(file, fadeIn));
                });
                __result = Nothing();
                return false;
        }
        // A photo the camera just took here: it goes into the house as a shared file.
        if (given && PhotoShare.Taken(__instance, ref filePath))
            return true;
        if (LoadRules.FromOtherGame(path))
        {
            __instance.storedImagePath = "";
            __result = Nothing();
            return false;
        }
        if (!given && !LoadRules.MayRead(path))
        {
            __result = Nothing();
            return false;
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(WallpaperPicker), nameof(WallpaperPicker.LoadWallpaper))]
    private static bool Wallpaper(WallpaperPicker __instance, ref string imgPath, ref IEnumerator __result)
    {
        switch (SharedFiles.Resolve(imgPath, out string local))
        {
            case Resolved.Ready:
                imgPath = local;
                return true;
            case Resolved.Pending:
                var picker = __instance;
                SharedFiles.WhenReady(imgPath, "the computer's wallpaper", file =>
                {
                    if (picker != null)
                        ItemFiles.StartOn(picker, picker.LoadWallpaper(file));
                });
                __result = Nothing();
                return false;
        }
        if (WallpaperShare.Picking || LoadRules.MayRead(imgPath))
            return true;
        __result = Nothing();
        return false;
    }

    private static IEnumerator Nothing()
    {
        yield break;
    }
}
