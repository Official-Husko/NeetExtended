using System;
using System.Collections.Generic;
using System.IO;
using Coop.Assets;
using Coop.Assets.Transfer;
using Coop.Net;
using Coop.Session;
using HarmonyLib;
using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.Video;

namespace Coop.World.Ambience;

/// <summary>
/// Videos on the TV (Custom TV Stations, or any channel that plays a VideoPlayer) run at the host's
/// pace. The TV's power and channel are shared state already. When a video comes on screen the host
/// holds it on its first frame until every player in the house has it loaded (a player who's still
/// getting the file holds everyone up; one who can't get it at all doesn't), then starts it for
/// everyone. While it plays, the host sends its time once a second, and a client more than half a
/// second off jumps to it (plus half the round trip), then lets it settle for a moment.
///
/// WorldTv, by kind:
/// - <see cref="State"/> (Stream, host to clients, once a second and on every change): channel
///   (short, -1 for no video), showing number (int, new each time a video comes on screen),
///   <see cref="Mode"/>, video time (double).
/// - <see cref="Ready"/> (Events, client to host): showing number, and whether this game has the
///   video loaded or can't get it.
/// </summary>
internal static class TvPlayback
{
    private const byte State = 0;
    private const byte Ready = 1;

    private enum Mode : byte
    {
        None,
        Held,
        Playing,
    }

    private const byte Loaded = 0;
    private const byte CantHave = 1;

    private const float SendEvery = 1f;
    private const double Tolerance = 0.5;
    private const double SeekLead = 0.15;
    private const float Settle = 2f;
    private const float Stale = 4f;
    // Nobody waits longer than this for a file that's stuck somewhere.
    private const float MaxHold = 180f;

    private static readonly AccessTools.FieldRef<TV, ScreenModule[]> Channels =
        AccessTools.FieldRefAccess<TV, ScreenModule[]>("channels");

    private static readonly Dictionary<byte, int> ReadyFor = new Dictionary<byte, int>();

    private static ScreenModule _module;
    private static VideoPlayer _player;

    // Host.
    private static int _showing = new System.Random().Next();
    private static ScreenModule _showingModule;
    private static Mode _mode;
    private static float _heldSince;
    private static float _next;
    private static Mode _sentMode;
    private static int _sentShowing;

    // Client: the host's last report.
    private static int _channel = -1;
    private static int _hostShowing;
    private static Mode _hostMode;
    private static double _time;
    private static float _heardAt = -100f;
    private static float _settleUntil;
    private static int _reported;

    public static void Init() => Messages.On(MessageIds.WorldTv, OnTv);

    public static void Clear()
    {
        _module = null;
        _player = null;
        _showingModule = null;
        _mode = Mode.None;
        _sentMode = Mode.None;
        ReadyFor.Clear();
        _channel = -1;
        _hostMode = Mode.None;
        _heardAt = -100f;
        _reported = 0;
    }

    public static void LateTick(bool host)
    {
        var tv = HouseManager._ != null ? HouseManager._.tv : null;
        if (tv == null)
            return;
        var player = Shown(tv, out int channel);
        if (host)
            Host(player, channel);
        else
            Follow(player, channel);
    }

    /// <summary>For the bridge: what this game's TV video is doing.</summary>
    public static string Describe()
    {
        var tv = HouseManager._ != null ? HouseManager._.tv : null;
        var player = tv != null ? Shown(tv, out _) : null;
        if (player == null)
            return "No video on the TV.";
        string state = player.isPlaying ? "playing" : player.isPrepared ? "paused" : "not loaded";
        return $"channel {tv.displayedScreenModule}: {state} at {player.time:0.00} of {player.length:0.00} s" + (WorldSide.IsHost
            ? $"; {_mode}, {ReadyCount()} ready"
            : $"; host said {_hostMode} channel {_channel} at {_time:0.00} s, {Time.unscaledTime - _heardAt:0.0} s ago");
    }

    // The video on screen, or null.
    private static VideoPlayer Shown(TV tv, out int channel)
    {
        channel = -1;
        if (!tv.isOn)
            return null;
        var channels = Channels(tv);
        int index = tv.displayedScreenModule;
        if (channels == null || index < 0 || index >= channels.Length || channels[index] == null)
            return null;
        var module = channels[index];
        if (module != _module)
        {
            _module = module;
            _player = module.GetComponent<VideoPlayer>();
        }
        channel = index;
        return _player != null && _player.isActiveAndEnabled ? _player : null;
    }

    private static void Host(VideoPlayer player, int channel)
    {
        float now = Time.unscaledTime;
        if (player == null)
        {
            _showingModule = null;
            _mode = Mode.None;
        }
        else if (_module != _showingModule)
        {
            // A video just came on screen: everyone gets it ready first.
            _showingModule = _module;
            _showing++;
            _mode = Mode.Held;
            _heldSince = now;
            ReadyFor.Clear();
        }

        if (_mode == Mode.Held)
        {
            HoldAtStart(player);
            bool hereReady = player.isPrepared || !CanHave(player);
            if ((hereReady && EveryoneReady()) || now - _heldSince > MaxHold)
            {
                if (now - _heldSince > MaxHold)
                    CoopMod.Log.Info("TV: started the video without everyone; somebody's copy didn't load in time.");
                _mode = Mode.Playing;
                if (player.isPrepared && player.canSetTime)
                    player.time = 0;
                player.Play();
            }
        }

        if (now < _next && _mode == _sentMode && _showing == _sentShowing)
            return;
        _next = now + SendEvery;
        _sentMode = _mode;
        _sentShowing = _showing;
        var w = Outbox.Begin(MessageIds.WorldTv);
        w.Put(State);
        w.Put((short)(_mode == Mode.None ? -1 : channel));
        w.Put(_showing);
        w.Put((byte)_mode);
        w.Put(_mode == Mode.Playing ? player.time : 0d);
        Outbox.ToPlayers(w, Channel.Stream);
    }

    private static bool EveryoneReady()
    {
        foreach (var p in CoopSession.Players)
        {
            if (p.IsLocal || !p.InWorld)
                continue;
            if (!ReadyFor.TryGetValue(p.Slot, out int showing) || showing != _showing)
                return false;
        }
        return true;
    }

    private static int ReadyCount()
    {
        int count = 0;
        foreach (var pair in ReadyFor)
        {
            if (pair.Value == _showing)
                count++;
        }
        return count;
    }

    private static void Follow(VideoPlayer player, int channel)
    {
        if (player == null || channel != _channel || _hostMode == Mode.None)
            return;
        float now = Time.unscaledTime;
        if (_hostMode == Mode.Held)
        {
            HoldAtStart(player);
            if (_reported != _hostShowing && (player.isPrepared || !CanHave(player)))
            {
                _reported = _hostShowing;
                var w = Outbox.Begin(MessageIds.WorldTv);
                w.Put(Ready);
                w.Put(_hostShowing);
                w.Put(player.isPrepared ? Loaded : CantHave);
                Outbox.ToHost(w, Channel.Events);
            }
            return;
        }

        // Playing: start it if it was held here (it starts with the next correction).
        if (!player.isPlaying && File.Exists(player.url))
        {
            player.Play();
            _settleUntil = 0f;
        }
        if (!player.isPrepared || !player.isPlaying || now - _heardAt > Stale || now < _settleUntil || !player.canSetTime)
            return;

        double length = player.length;
        double target = _time + (now - _heardAt) + Latency();
        if (length > 0)
        {
            if (player.isLooping)
                target %= length;
            else if (target >= length)
                return;
        }
        double off = player.time - target;
        // Looping: just before the end and just after the start are close.
        if (length > 0 && player.isLooping)
        {
            if (off > length / 2)
                off -= length;
            else if (off < -length / 2)
                off += length;
        }
        if (Math.Abs(off) <= Tolerance)
            return;
        player.time = length > 0 && player.isLooping ? (target + SeekLead) % length : target + SeekLead;
        _settleUntil = now + Settle;
    }

    // On its first frame until everyone has it: loaded (Pause loads it) but not running.
    private static void HoldAtStart(VideoPlayer player)
    {
        if (player.isPlaying)
            player.Pause();
        else if (!player.isPrepared && File.Exists(player.url))
            player.Pause();
        if (player.isPrepared && player.canSetTime && player.time > 0.05)
            player.time = 0;
    }

    // Whether this game has the video, or will once a download that's going finishes.
    private static bool CanHave(VideoPlayer player)
    {
        string url = player.url;
        if (string.IsNullOrEmpty(url))
            return false;
        if (File.Exists(url))
            return true;
        return SharedPaths.TryParse(url, out string hash, out string ext) && Downloads.IsGetting(hash + ext);
    }

    private static double Latency()
    {
        int rtt = CoopSession.Transport != null ? CoopSession.Transport.PingOf(CoopSession.HostPeer) : -1;
        return rtt > 0 ? rtt / 2000.0 : 0.0;
    }

    private static void OnTv(in Sender from, NetDataReader reader)
    {
        if (!WorldSide.Running)
            return;
        byte kind = reader.GetByte();
        if (kind == Ready)
        {
            if (WorldSide.IsHost)
            {
                int showing = reader.GetInt();
                reader.GetByte();
                ReadyFor[from.Slot] = showing;
            }
            return;
        }
        if (kind != State || WorldSide.IsHost)
            return;
        int channel = reader.GetShort();
        int number = reader.GetInt();
        var mode = (Mode)reader.GetByte();
        double time = reader.GetDouble();
        if (number != _hostShowing || mode != _hostMode)
            _settleUntil = 0f; // A new video, or it just started: line it up straight away.
        _channel = channel;
        _hostShowing = number;
        _hostMode = mode;
        _time = time;
        _heardAt = Time.unscaledTime;
    }
}
