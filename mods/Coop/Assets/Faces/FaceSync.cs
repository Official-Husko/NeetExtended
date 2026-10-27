using System.Collections.Generic;
using System.IO;
using Coop.Assets.Transfer;
using Coop.Net;
using Coop.Players;
using Coop.Session;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Assets.Faces;

/// <summary>
/// Everyone's face on their body in the other games. The game reads the player's face from
/// StreamingAssets/Faces/Face.png or .jpg (the computer's face changer replaces that file), so each
/// game offers its own file and tells the others its id; they fetch it and put it on that player's
/// body. A new face (changed on the computer) goes out the same way.
///
/// AssetFace (Events): slot, file id (none: the default face). Clients send their own; the host
/// passes them on and sends everyone's to a player who just came in. From the host with slot
/// <see cref="Outbox.NoSlot"/>: send yours again (it hot reloaded and forgot them).
/// </summary>
internal static class FaceSync
{
    private const float CheckInterval = 0.5f;

    private static readonly Dictionary<byte, AssetId> Known = new Dictionary<byte, AssetId>();
    private static readonly Dictionary<byte, Texture2D> Faces = new Dictionary<byte, Texture2D>();
    private static readonly Dictionary<byte, PlayerPuppet> ShownOn = new Dictionary<byte, PlayerPuppet>();
    private static readonly List<byte> Slots = new List<byte>();
    private static AssetId _local;
    private static bool _stale = true;
    private static bool _hashing;
    private static bool _sent;
    private static float _nextCheck;

    /// <summary>Tests: a picture used as this game's face instead of the game's face file.</summary>
    public static string TestFace { get; set; }

    public static void Init()
    {
        Messages.On(MessageIds.AssetFace, OnFace);
        CoopSession.WorldReady += Restart;
        CoopSession.Resumed += () =>
        {
            Restart();
            if (CoopSession.IsHost)
                Send(Outbox.NoSlot, default, Outbox.NoSlot);
        };
        CoopSession.PlayerReady += OnPlayerReady;
        CoopSession.PlayerLeft += player => Forget(player.Slot);
        CoopSession.Ended += _ => ForgetAll();
        CoopLoop.Unloading += ForgetAll;
    }

    /// <summary>From PlayerStats.SetPlayerFace: this player's face may have changed.</summary>
    public static void FaceSet(PlayerStats stats)
    {
        var player = HouseManager._ != null ? HouseManager._.player : null;
        if (player != null && stats != null && stats.gameObject == player)
            _stale = true;
    }

    /// <summary>This player's face file may be another one now.</summary>
    public static void Changed() => _stale = true;

    public static string Describe(byte slot) => Known.TryGetValue(slot, out var id) ? id.ToString() + (Faces.ContainsKey(slot) ? ", shown" : ", on its way") : "default";

    public static AssetId Local => _local;

    public static void Tick()
    {
        if (!CoopSession.InWorld || Time.unscaledTime < _nextCheck)
            return;
        _nextCheck = Time.unscaledTime + CheckInterval;

        if (_stale && !_hashing)
            HashLocal();
        if (!_sent && !_stale && !_hashing && CoopSession.Local != null)
        {
            _sent = true;
            Send(CoopSession.Local.Slot, _local, Outbox.NoSlot);
        }

        // Bodies are built when a player's first pose arrives, and again after some changes.
        Slots.Clear();
        Slots.AddRange(Faces.Keys);
        foreach (byte slot in Slots)
        {
            var puppet = RemotePlayers.Find(slot);
            if (puppet == null || (ShownOn.TryGetValue(slot, out var shown) && shown == puppet))
                continue;
            PuppetFace.Show(puppet, Faces[slot]);
            ShownOn[slot] = puppet;
        }
    }

    private static void Restart()
    {
        _stale = true;
        _sent = false;
    }

    private static void OnPlayerReady(CoopPlayer player)
    {
        _sent = false;
        if (!CoopSession.IsHost)
            return;
        foreach (var pair in Known)
        {
            if (pair.Key != player.Slot)
                Send(pair.Key, pair.Value, Outbox.NoSlot, player.Slot);
        }
    }

    private static void HashLocal()
    {
        string path = LocalFacePath();
        if (path == null)
        {
            _local = default;
            _stale = false;
            _sent = false;
            return;
        }
        _hashing = true;
        FileHasher.Hash(path, id =>
        {
            _hashing = false;
            _stale = false;
            if (id.IsValid)
                LocalFiles.Add(id, path);
            if (!id.Equals(_local))
                _sent = false;
            _local = id;
        });
    }

    // Where the game reads this player's face (PlayerStats.GetFaceTexture).
    private static string LocalFacePath()
    {
        if (!string.IsNullOrEmpty(TestFace))
            return File.Exists(TestFace) ? TestFace : null;
        var player = HouseManager._ != null ? HouseManager._.player : null;
        var stats = player != null ? player.GetComponent<PlayerStats>() : null;
        string name = stats != null ? stats.faceTextureName : "Face";
        if (string.IsNullOrEmpty(name))
            return null;
        string folder = Path.Combine(Application.streamingAssetsPath, "Faces");
        string png = Path.Combine(folder, name + ".png");
        if (File.Exists(png))
            return png;
        string jpg = Path.Combine(folder, name + ".jpg");
        return File.Exists(jpg) ? jpg : null;
    }

    private static void Send(byte slot, in AssetId id, byte except, byte only = Outbox.NoSlot)
    {
        var w = Outbox.Begin(MessageIds.AssetFace);
        w.Put(slot);
        id.Write(w);
        if (only != Outbox.NoSlot)
            Outbox.ToSlot(only, w, Channel.Events);
        else
            Outbox.ToPlayers(w, Channel.Events, except);
    }

    private static void OnFace(in Sender from, NetDataReader reader)
    {
        byte slot = reader.GetByte();
        var id = AssetId.Read(reader);
        if (slot == Outbox.NoSlot)
        {
            if (CoopSession.IsClient)
                _sent = false;
            return;
        }
        if (id.IsValid && (!id.SizeKnown || id.Size > FileTypes.MaxBytes(id.Ext) || !FileTypes.TryKind(id.Ext, out var kind) || kind != FileKind.Image))
            id = default;
        if (CoopSession.IsHost)
        {
            slot = from.Slot; // A client only speaks for itself.
            Send(slot, id, slot);
            Hub.Offered(id, slot, (CoopSession.FindSlot(slot)?.Name ?? "a player") + "'s face");
        }
        if (slot == CoopSession.Local?.Slot)
            return;
        Show(slot, id);
    }

    private static void Show(byte slot, AssetId id)
    {
        if (Known.TryGetValue(slot, out var known) && known.Equals(id))
            return;
        Forget(slot);
        Known[slot] = id;
        if (!id.IsValid)
        {
            PuppetFace.Show(RemotePlayers.Find(slot), null);
            return;
        }
        string who = (CoopSession.FindSlot(slot)?.Name ?? "player " + slot) + "'s face";
        Downloads.Want(id, who, path =>
        {
            if (CoopMod.Instance != null)
                CoopMod.Instance.StartCoroutine(FaceLoader.Load(path, texture => Loaded(slot, id, texture)));
        });
    }

    private static void Loaded(byte slot, AssetId id, Texture2D texture)
    {
        if (texture == null)
            return;
        if (!Known.TryGetValue(slot, out var current) || !current.Equals(id))
        {
            Object.Destroy(texture);
            return;
        }
        if (Faces.TryGetValue(slot, out var old) && old != null)
            Object.Destroy(old);
        Faces[slot] = texture;
        ShownOn.Remove(slot);
    }

    private static void Forget(byte slot)
    {
        Known.Remove(slot);
        ShownOn.Remove(slot);
        if (Faces.TryGetValue(slot, out var texture))
        {
            PuppetFace.Show(RemotePlayers.Find(slot), null);
            if (texture != null)
                Object.Destroy(texture);
            Faces.Remove(slot);
        }
    }

    private static void ForgetAll()
    {
        Slots.Clear();
        Slots.AddRange(Known.Keys);
        foreach (byte slot in Slots)
            Forget(slot);
        Faces.Clear();
        _sent = false;
        _stale = true;
    }
}
