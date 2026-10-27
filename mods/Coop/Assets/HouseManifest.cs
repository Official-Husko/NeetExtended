using System.Collections.Generic;
using System.IO;
using Coop.Assets.Faces;
using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Assets;

/// <summary>
/// Host: the files the house shows right now (custom pictures and tapes, photos, the computer's
/// wallpaper, the players' faces), sent to a joining player as soon as it's let in, so it can get
/// them before its house loads (<see cref="Join.JoinFiles"/>). The TV stations and tapes come with
/// the media list.
///
/// AssetManifest (Events, host to a joiner): count, then per file: id (size -1 if the host doesn't
/// have it yet), label.
/// </summary>
internal static class HouseManifest
{
    public static void Init()
    {
        Messages.On(MessageIds.AssetManifest, OnManifest);
        CoopSession.PlayerArrived += Send;
    }

    private static void Send(CoopPlayer player)
    {
        if (!CoopSession.IsHost)
            return;
        var files = Collect(player.Slot);
        var w = Outbox.Begin(MessageIds.AssetManifest);
        w.Put((ushort)files.Count);
        foreach (var file in files)
        {
            file.Key.Write(w);
            w.Put(file.Value);
        }
        Outbox.ToSlot(player.Slot, w, Channel.Events);
    }

    private static List<KeyValuePair<AssetId, string>> Collect(byte joiner)
    {
        var files = new List<KeyValuePair<AssetId, string>>();
        var seen = new HashSet<string>();
        if (CoopSession.InWorld)
        {
            foreach (var picture in Object.FindObjectsOfType<CustomTextureSaveLoader>())
                Add(files, seen, picture.persistentTexturePath, "a custom picture");
            foreach (var tape in Object.FindObjectsOfType<CustomMusicSaveLoader>())
                Add(files, seen, tape.persistentMusicPath, "a custom tape");
            foreach (var photo in Object.FindObjectsOfType<Picture>())
                Add(files, seen, photo.storedImagePath, "a photo");
            var computer = HouseManager._ != null ? HouseManager._.mainComputer : null;
            if (computer != null)
                Add(files, seen, computer.wallpaperImgPath, "the computer's wallpaper");
        }
        if (FaceSync.Local.IsValid && seen.Add(FaceSync.Local.FileName))
            files.Add(new KeyValuePair<AssetId, string>(FaceSync.Local, "the host's face"));
        foreach (var face in FaceSync.Others)
        {
            if (face.Key != joiner && face.Value.IsValid && seen.Add(face.Value.FileName))
                files.Add(new KeyValuePair<AssetId, string>(face.Value, (CoopSession.FindSlot(face.Key)?.Name ?? "a player") + "'s face"));
        }
        return files;
    }

    private static void Add(List<KeyValuePair<AssetId, string>> files, HashSet<string> seen, string path, string label)
    {
        if (!SharedPaths.TryParse(path, out string hash, out string ext) || !seen.Add(hash + ext))
            return;
        string local = LocalFiles.Find(hash, ext);
        long size = -1;
        if (local != null)
        {
            try { size = new FileInfo(local).Length; }
            catch (IOException) { }
        }
        files.Add(new KeyValuePair<AssetId, string>(new AssetId(hash, size, ext), label));
    }

    private static void OnManifest(in Sender from, NetDataReader reader)
    {
        if (!CoopSession.IsClient || from.Slot != CoopPlayer.HostSlot)
            return;
        int count = reader.GetUShort();
        var files = new List<KeyValuePair<AssetId, string>>(count);
        for (int i = 0; i < count; i++)
        {
            var id = AssetId.Read(reader);
            string label = reader.GetString(128);
            if (id.IsValid && FileTypes.IsMedia(id.Ext))
                files.Add(new KeyValuePair<AssetId, string>(id, label));
        }
        Join.JoinFiles.SetManifest(files);
    }
}
