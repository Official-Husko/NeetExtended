using System.IO;
using System.Text;
using Coop.Assets.Faces;
using Coop.Assets.Media;
using Coop.Assets.Transfer;
using Coop.Session;
using Coop.World.Objects;
using HarmonyLib;
using NeetModKit.Game;
using UnityEngine;

namespace Coop.Assets;

/// <summary>
/// File sharing from the dev bridge: status, and test hands that go through the game's own code
/// (<c>neet call type.call type=Coop.Assets.AssetsApi method=Status --instance 2</c>).
/// </summary>
public static class AssetsApi
{
    public static string Status()
    {
        var text = new StringBuilder();
        text.Append("cache ").Append(FileCache.Folder).Append(", offered ").Append(LocalFiles.OfferedCount)
            .Append(LocalFiles.IgnoreOwnForTests ? " (ignored for tests)" : "")
            .Append(", media ").Append(LocalMedia.IsReady ? "ready" : "hashing")
            .Append(", my face ").Append(FaceSync.Local);
        foreach (var download in Downloads.Active)
            text.Append("\n  getting ").Append(download.Describe()).Append(": ").Append(FileSizes.Describe(download.Received))
                .Append(download.Asked ? $" from slot {download.Source}" : " (waiting for a source)").Append(download.Verifying ? ", checking" : "");
        text.Append("\n  sending ").Append(Uploads.Count);
        foreach (var player in CoopSession.Players)
        {
            if (!player.IsLocal)
                text.Append("\n  face of slot ").Append(player.Slot).Append(": ").Append(FaceSync.Describe(player.Slot));
        }
        return text.ToString();
    }

    /// <summary>The session's TV stations and tapes as this game shows them: each folder, whose it is, and how many files are here.</summary>
    public static string Media() => MediaStandIn.Describe();

    /// <summary>The TV's video here, and what the host last said about it.</summary>
    public static string Tv() => World.Ambience.TvPlayback.Describe();

    private static int _testFolders;

    /// <summary>Tests: puts a folder of this game's own into the session ("tv" or "mixtapes"), as if a mod had it.</summary>
    public static string AddMedia(string folderName, string path)
    {
        if (!Directory.Exists(path))
            return "No such folder.";
        LocalMedia.AddForTests("CoopTest" + folderName + (++_testFolders), folderName, path);
        return "Added; it's hashed and sent in a moment.";
    }

    /// <summary>Tests: a notice in the corner, to look at.</summary>
    public static string TestNotice(string title, string detail, float progress, float seconds)
    {
        NeetModKit.Ui.Notifications.Notices.Show(CoopMod.Instance, title, detail, progress >= 0f ? progress : (float?)null, seconds);
        return "Shown.";
    }

    /// <summary>Tests: this game uses only what it got sent (its cache), not its own copies of the same files.</summary>
    public static string ReceiveOnly(bool on)
    {
        LocalFiles.IgnoreOwnForTests = on;
        return on ? "Using only received files." : "Using own files too.";
    }

    /// <summary>Tests: empties this game's cache folder.</summary>
    public static string ClearCache()
    {
        Downloads.Clear();
        int removed = 0;
        foreach (string file in Directory.GetFiles(FileCache.Folder))
        {
            string name = Path.GetFileName(file);
            if (AssetId.IsHash(name.Split('.')[0]))
            {
                File.Delete(file);
                removed++;
            }
        }
        return $"Removed {removed} file(s).";
    }

    /// <summary>Client: asks the host for its media folders again.</summary>
    public static string AskMedia()
    {
        MediaSync.Ask();
        return "Asked.";
    }

    /// <summary>Tests: shows this picture as this player's face to the others ("" for the game's face file).</summary>
    public static string UseFace(string path)
    {
        FaceSync.TestFace = path;
        FaceSync.Changed();
        return "Face: " + (string.IsNullOrEmpty(path) ? "the game's" : path);
    }

    /// <summary>Tests: buys from a custom shop on the computer with this file, the way its buy button does.</summary>
    public static string OrderCustom(string path, bool song)
    {
        var computer = HouseManager._ != null ? HouseManager._.mainComputer : null;
        if (computer == null)
            return "No computer.";
        foreach (var shop in computer.GetComponentsInChildren<CustomItemShop>(true))
        {
            if (shop.filterForMusic != song)
                continue;
            shop.latestFilePath = path;
            if (!song && File.Exists(path))
            {
                var texture = new Texture2D(2, 2);
                texture.LoadImage(File.ReadAllBytes(path));
                shop.previewImg.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            AccessTools.Method(typeof(CustomItemShop), nameof(CustomItemShop.CacheItemToOrder)).Invoke(shop, new object[] { 0, song ? 1 : 0 });
            shop.confirmBuyButton.onClick.Invoke();
            return $"Ordered a custom {(song ? "tape" : "picture")}; the shop's file is now {shop.latestFilePath}.";
        }
        return "No such shop.";
    }

    /// <summary>Tests: opens every delivered box that unpacks a custom item.</summary>
    public static string OpenCustomBoxes()
    {
        int opened = 0;
        foreach (var box in Object.FindObjectsOfType<Cardboard_Box>())
        {
            if (box.content == null || box.OnUnboxingApplyToContents == null)
                continue;
            box.taped = false;
            box.Use();
            opened++;
        }
        return $"Opened {opened} box(es).";
    }

    /// <summary>Tests: puts a photo camera in this player's hand and takes a photo.</summary>
    public static string TakePhoto(string cameraItem)
    {
        var abilities = HouseManager._?.player?.GetComponent<Player_Abilities>();
        if (abilities == null || !Spawning.IsItem(cameraItem))
            return abilities == null ? "No player." : $"No item {cameraItem}.";
        var held = abilities.pickups[0];
        var camera = held != null ? held.GetComponent<PhotoCamera>() : null;
        if (camera == null)
        {
            var go = Spawning.ItemNearPlayer(cameraItem);
            abilities.Pickup((global::Hands)0, go);
            camera = go.GetComponent<PhotoCamera>();
        }
        if (camera == null)
            return "That isn't a camera.";
        camera.Use();
        return "Click.";
    }

    /// <summary>Tests: picks a wallpaper on the computer, like its file browser does.</summary>
    public static string PickWallpaper(string path)
    {
        var picker = HouseManager._?.mainComputer?.GetComponentInChildren<WallpaperPicker>(true);
        if (picker == null)
            return "No wallpaper picker.";
        AccessTools.Method(typeof(WallpaperPicker), "CopyFilesToStreamingAssets").Invoke(picker, new object[] { new[] { path } });
        return "Picked.";
    }

    /// <summary>What a shared item shows: its file path and whether a picture or song is on it.</summary>
    public static string Item(int itemId)
    {
        var o = NetObjects.Find(new NetRef(NetKind.Item, itemId));
        if (o == null || !o.Alive)
            return $"No shared item {itemId}.";
        var text = new StringBuilder(o.Go.name);
        var picture = o.Go.GetComponentInChildren<CustomTextureSaveLoader>(true);
        if (picture != null)
        {
            var renderer = picture.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var shown = block.GetTexture("_MainTex") ?? renderer.material.GetTexture("_MainTex");
            text.Append("\n  custom picture: ").Append(picture.persistentTexturePath).Append(", shows ").Append(Describe(shown));
        }
        var tape = o.Go.GetComponentInChildren<CustomMusicSaveLoader>(true);
        if (tape != null)
            text.Append("\n  custom tape: ").Append(tape.persistentMusicPath).Append(", song ")
                .Append(tape.cassette != null && tape.cassette.musicClip != null ? $"{tape.cassette.musicClip.length:0.0} s" : "none");
        var photo = o.Go.GetComponentInChildren<Picture>(true);
        if (photo != null)
            text.Append("\n  photo: ").Append(photo.storedImagePath).Append(", shows ").Append(Describe(photo.GetComponent<MeshRenderer>().materials[1].mainTexture));
        return text.ToString();
    }

    /// <summary>The newest shared items whose name contains <paramref name="filter"/>, by id.</summary>
    public static string Items(string filter)
    {
        var text = new StringBuilder();
        foreach (var o in NetObjects.All)
        {
            if (o.Alive && o.Ref.Kind == NetKind.Item && o.Go.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                text.Append(o.Ref.Id).Append(' ').Append(o.Go.name).Append('\n');
        }
        return text.Length > 0 ? text.ToString() : "None.";
    }

    public static string Wallpaper()
    {
        var computer = HouseManager._ != null ? HouseManager._.mainComputer : null;
        var picker = computer != null ? computer.GetComponentInChildren<WallpaperPicker>(true) : null;
        if (picker == null)
            return "No computer.";
        return computer.wallpaperImgPath + ", shows " + Describe(picker.wallpaperImg.sprite != null ? picker.wallpaperImg.sprite.texture : null);
    }

    /// <summary>The face texture on another player's body here.</summary>
    public static string FaceOn(int slot)
    {
        var puppet = Players.RemotePlayers.Find((byte)slot);
        if (puppet == null)
            return "No body for that slot.";
        foreach (var renderer in puppet.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.name == "Neet")
                return $"{renderer.sharedMaterials[3].name}: {Describe(renderer.sharedMaterials[3].mainTexture)}";
        }
        return "No body mesh.";
    }

    private static string Describe(Texture texture) =>
        texture == null ? "nothing" : $"{texture.name} {texture.width}x{texture.height}";
}
