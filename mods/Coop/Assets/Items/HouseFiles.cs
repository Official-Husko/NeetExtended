using System;
using Coop.Session;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Coop.Assets.Items;

/// <summary>
/// Host, when it starts hosting: things already in its house that show a file from its disk
/// (custom photos and tapes bought before, photos, the wallpaper) are turned into shared files in
/// the background, and each one's new path goes to the players. Players who join later get the
/// shared paths with the house. The host's save keeps them from then on.
/// </summary>
internal static class HouseFiles
{
    public static void ShareAll()
    {
        if (!CoopSession.IsHost)
            return;
        int count = 0;
        foreach (var picture in Object.FindObjectsOfType<CustomTextureSaveLoader>())
            count += Convert(picture, picture.persistentTexturePath, ItemPaths.Texture, (p, path) => p.persistentTexturePath = path, p => p.persistentTexturePath);
        foreach (var tape in Object.FindObjectsOfType<CustomMusicSaveLoader>())
            count += Convert(tape, tape.persistentMusicPath, ItemPaths.Music, (t, path) => t.persistentMusicPath = path, t => t.persistentMusicPath);
        foreach (var photo in Object.FindObjectsOfType<Picture>())
            count += Convert(photo, photo.storedImagePath, ItemPaths.Photo, (p, path) => p.storedImagePath = path, p => p.storedImagePath);

        var computer = HouseManager._ != null ? HouseManager._.mainComputer : null;
        string wallpaper = computer != null ? computer.wallpaperImgPath : null;
        if (SharedPaths.IsLocalFile(wallpaper))
        {
            count++;
            SharedFiles.ShareLater(wallpaper, shared =>
            {
                if (shared == null || computer == null || computer.wallpaperImgPath != wallpaper)
                    return;
                computer.wallpaperImgPath = shared;
                ItemPaths.AnnounceWallpaper(shared);
            });
        }
        if (count > 0)
            CoopMod.Log.Info($"Files: sharing {count} file(s) already in the house (custom items, photos, wallpaper).");
    }

    private static int Convert<T>(T part, string path, byte kind, Action<T, string> set, Func<T, string> get) where T : Component
    {
        if (!SharedPaths.IsLocalFile(path))
            return 0;
        SharedFiles.ShareLater(path, shared =>
        {
            // Skipped if it changed meanwhile (or the item is gone).
            if (shared == null || part == null || get(part) != path)
                return;
            set(part, shared);
            ItemPaths.Announce(part, kind, shared);
        });
        return 1;
    }
}
