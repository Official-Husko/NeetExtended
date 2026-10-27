using System.Text;
using Coop.Assets.Faces;
using Coop.Assets.Items;
using Coop.Assets.Media;
using Coop.Assets.Transfer;
using Coop.Session;
using UnityEngine;

namespace Coop.Assets;

/// <summary>
/// Files from players' disks, shared in co-op: custom photos and tapes from the custom shops, photos
/// from the camera, the computer's wallpaper, faces, and the host's TV stations and mixtapes. Each
/// file is known by its content (<see cref="AssetId"/>), sent once in the background and kept in
/// the cache; until it's there, the game shows what it shows without it. This wires the parts up.
/// </summary>
internal static class FileSharing
{
    public static void Init(CoopMod mod)
    {
        FileCache.Init(mod.Info.Directory);
        HashIndex.Load();
        Downloads.Init();
        Uploads.Init();
        Hub.Init();
        ItemPaths.Init();
        FaceSync.Init();
        MediaSync.Init(mod);

        CoopLoop.Update += Update;
        CoopLoop.LateUpdate += ItemPaths.LateTick;
        CoopLoop.Unloading += Stop;
        CoopSession.PlayerLeft += player =>
        {
            Uploads.DropSlot(player.Slot);
            Downloads.SourceLeft(player.Slot);
            Hub.SlotLeft(player.Slot);
        };
        CoopSession.Ended += _ => Stop();
        CoopSession.WorldReady += () =>
        {
            if (CoopSession.IsHost)
                HouseFiles.ShareAll();
        };
        // Whatever was waiting for a file in the last copy of the mod waits again.
        CoopSession.Resumed += () =>
        {
            AssetNet.SayRestarted();
            if (CoopSession.IsHost)
                HouseFiles.ShareAll();
            WaitAgain();
        };

        FileCache.RemoveStaleParts(Downloads.Parts());
        FileCache.Trim();
    }

    /// <summary>One line for the Co-op tab: what's being sent and fetched, or "" when nothing is.</summary>
    public static string StatusLine()
    {
        var downloads = Downloads.Active;
        int sending = Uploads.Count;
        if (downloads.Count == 0 && sending == 0)
            return "";
        var line = new StringBuilder("Files: ");
        if (downloads.Count > 0)
        {
            Download biggest = null;
            foreach (var download in downloads)
            {
                if (biggest == null || download.Total > biggest.Total)
                    biggest = download;
            }
            line.Append("getting ").Append(downloads.Count).Append(downloads.Count == 1 ? " file" : " files");
            if (biggest != null && biggest.Total > 0)
                line.Append(" (").Append(biggest.Label).Append(": ")
                    .Append(FileSizes.Describe(biggest.Received)).Append(" of ").Append(FileSizes.Describe(biggest.Total)).Append(')');
        }
        if (sending > 0)
            line.Append(downloads.Count > 0 ? ", " : "").Append("sending ").Append(sending);
        return line.ToString();
    }

    private static void Update()
    {
        FileHasher.Tick();
        if (!CoopSession.IsActive)
            return;
        Downloads.Tick();
        Uploads.Tick();
        FaceSync.Tick();
        MediaSync.Tick();
    }

    private static void Stop()
    {
        Downloads.Clear();
        Uploads.Clear();
        Hub.Clear();
        ItemPaths.Clear();
    }

    private static void WaitAgain()
    {
        if (!CoopSession.InWorld)
            return;
        foreach (var picture in Object.FindObjectsOfType<CustomTextureSaveLoader>())
        {
            var p = picture;
            Wait(p.persistentTexturePath, "a custom picture", file => ItemFiles.LoadPicture(p, p.gameObject, file));
        }
        foreach (var tape in Object.FindObjectsOfType<CustomMusicSaveLoader>())
        {
            var t = tape;
            Wait(t.persistentMusicPath, "a custom tape", file => t.CustomizeItem(t.gameObject, file));
        }
        foreach (var photo in Object.FindObjectsOfType<Picture>())
        {
            var p = photo;
            Wait(p.storedImagePath, "a photo", file => ItemFiles.StartOn(p, p.loadImage(file, false)));
        }
        var computer = HouseManager._ != null ? HouseManager._.mainComputer : null;
        var picker = computer != null ? computer.GetComponentInChildren<WallpaperPicker>(true) : null;
        if (picker != null)
            Wait(computer.wallpaperImgPath, "the computer's wallpaper", file => ItemFiles.StartOn(picker, picker.LoadWallpaper(file)));
    }

    private static void Wait(string path, string label, System.Action<string> load)
    {
        if (SharedFiles.Resolve(path, out _) == Resolved.Pending)
            SharedFiles.WhenReady(path, label, file =>
            {
                try { load(file); }
                catch (System.Exception e) { CoopMod.Log.Warn($"Files: loading {label} failed: {e.Message}"); }
            });
    }
}
