using System;
using Coop.Assets.Faces;
using Coop.Assets.Items;
using Coop.Session;
using Coop.World.Economy;
using HarmonyLib;
using UnityEngine;

namespace Coop.Assets.Hooks;

/// <summary>
/// Where a file from this player's disk goes into the shared house during a session: a custom shop
/// order, a photo (see <see cref="PhotoShare"/>), a wallpaper, the face. The file is copied into the
/// cache and the house keeps its shared path from then on. Outside a session these do nothing.
/// </summary>
[HarmonyPatch]
internal static class EntryPatches
{
    // Before Co-op's own order prefix (Priority.First), which reads the shop's file for the host.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First + 1)]
    [HarmonyPatch(typeof(Computer), nameof(Computer.OrderItem))]
    private static void Order(Action<GameObject> OnUnboxingApplyToContents)
    {
        if (!CoopSession.IsActive || OnUnboxingApplyToContents == null)
            return;
        var shop = BoxCustomization.ShopOf(OnUnboxingApplyToContents.Target);
        if (shop != null)
            shop.latestFilePath = SharedFiles.Share(shop.latestFilePath, shop.filterForMusic ? "a custom tape" : "a custom picture");
    }

    // Picking a file in a custom shop: copied in the background, so buying it doesn't wait.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomItemShop), "LoadImagePreview")]
    private static void PickedPicture(string[] paths) => Prepare(paths);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomItemShop), "LoadFilePathPreview")]
    private static void PickedSong(string[] paths) => Prepare(paths);

    // The game's own unboxing of a custom item ordered on this machine (the file may have been picked
    // after the order, or the box ordered before the session).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomItemShop), "CustomizeItem")]
    private static void UnboxedPicture(GameObject obj)
    {
        var picture = CoopSession.IsActive && obj != null ? obj.GetComponentInChildren<CustomTextureSaveLoader>() : null;
        if (picture != null)
            picture.persistentTexturePath = SharedFiles.Share(picture.persistentTexturePath, "a custom picture");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomItemShop), "CustomizeMediaURLsOnItem")]
    private static void UnboxedTape(GameObject obj)
    {
        var tape = CoopSession.IsActive && obj != null ? obj.GetComponentInChildren<CustomMusicSaveLoader>() : null;
        if (tape != null)
            tape.persistentMusicPath = SharedFiles.Share(tape.persistentMusicPath, "a custom tape");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WallpaperPicker), "CopyFilesToStreamingAssets")]
    private static void PickingWallpaper(string[] paths) => WallpaperShare.Begin(paths);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(WallpaperPicker), "CopyFilesToStreamingAssets")]
    private static void PickedWallpaper() => WallpaperShare.End();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerStats), nameof(PlayerStats.SetPlayerFace))]
    private static void FaceSet(PlayerStats __instance) => FaceSync.FaceSet(__instance);

    private static void Prepare(string[] paths)
    {
        if (CoopSession.IsActive && paths != null && paths.Length > 0 && SharedPaths.IsLocalFile(paths[0]))
            FileHasher.Import(paths[0], null);
    }
}
