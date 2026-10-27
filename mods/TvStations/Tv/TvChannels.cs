using System;
using System.Collections.Generic;
using HarmonyLib;
using NeetModKit.Game;
using TvStations.Stations;
using UnityEngine;

namespace TvStations.Tv;

/// <summary>
/// The TV's channel list (TV.channels, filled from its children in Awake). Stations are put in it
/// as extra channels, in place of game channels they replace, and hidden game channels are left
/// out. The remote's six buttons pick channels 1 to 6 of the list, and hitting the remote moves to
/// the next one, so the order here is the order on the remote. The game's list is put back on
/// unload.
/// </summary>
internal static class TvChannels
{
    private static readonly AccessTools.FieldRef<TV, ScreenModule[]> Channels =
        AccessTools.FieldRefAccess<TV, ScreenModule[]>("channels");

    private static readonly Action<TV> DisableAllChannels =
        AccessTools.MethodDelegate<Action<TV>>(AccessTools.Method(typeof(TV), "DisableAllChannels"));

    private static readonly List<VideoChannel> Videos = new List<VideoChannel>();
    private static readonly List<Station> StationList = new List<Station>();
    private static readonly HashSet<string> HiddenChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static TV _tv;
    private static ScreenModule[] _gameChannels;
    private static bool _keepGameChannels = true;
    private static bool _active;

    /// <summary>The TV's current list, game channels and stations.</summary>
    public static IReadOnlyList<ScreenModule> Current => _tv != null ? Channels(_tv) : new ScreenModule[0];

    public static TV Tv => _tv != null ? _tv : (_tv = GameRefs.House != null ? GameRefs.House.tv : null);

    public static float Volume { get; set; } = 1f;

    public static void SetStations(IEnumerable<Station> stations, IEnumerable<string> hidden)
    {
        StationList.Clear();
        StationList.AddRange(stations);
        HiddenChannels.Clear();
        HiddenChannels.UnionWith(hidden);
        Rebuild();
    }

    public static void SetKeepGameChannels(bool keep)
    {
        _keepGameChannels = keep;
        Rebuild();
    }

    /// <summary>The TV just collected its channels (TV.Awake).</summary>
    public static void OnTvAwake(TV tv)
    {
        _tv = tv;
        _gameChannels = Channels(tv);
        Videos.Clear(); // A new scene: the old objects went with the old TV.
        Rebuild();
    }

    public static VideoChannel VideoFor(ScreenModule module)
    {
        foreach (var video in Videos)
        {
            if (video.Module == module)
                return video;
        }
        return null;
    }

    /// <summary>A station's video turned up (co-op got it): the station plays if it's on screen.</summary>
    public static void FileArrived(string path)
    {
        foreach (var video in Videos)
        {
            if (string.Equals(video.Station.File, path, StringComparison.OrdinalIgnoreCase))
                video.FileArrived();
        }
    }

    /// <summary>A game channel's name: "Channel5_Sports" is "Sports".</summary>
    public static string GameName(ScreenModule channel)
    {
        string name = channel.gameObject.name;
        int underscore = name.IndexOf('_');
        return underscore >= 0 ? name.Substring(underscore + 1) : name;
    }

    /// <summary>
    /// Shows a station when the TV switches to one (TV.Control for a channel number). Returns false
    /// when it handled the switch, true to let the game switch to one of its own channels.
    /// </summary>
    public static bool Control(TV tv, int remoteInput)
    {
        if (remoteInput < 0 || !tv.IsOn)
            return true;
        var channels = Channels(tv);
        if (channels.Length == 0)
            return true;
        // Same rule as the game: anything past the end goes back to the first channel.
        int index = remoteInput < channels.Length ? remoteInput : 0;
        var video = VideoFor(channels[index]);
        if (video == null)
            return true;

        DisableAllChannels(tv);
        tv.displayedScreenModule = index;
        tv.audioTv.loop = false;
        tv.audioTv.Stop();
        tv.audioTv.clip = null;
        video.Show(Volume);
        return false;
    }

    public static void ApplyVolume()
    {
        foreach (var video in Videos)
            video.SetVolume(Volume);
    }

    /// <summary>From now on, changes are applied to the TV.</summary>
    public static void Start() => _active = true;

    /// <summary>Puts the game's own list back, for unloading. Nothing is changed after this.</summary>
    public static void Restore()
    {
        _active = false;
        var tv = Tv;
        bool wasShowing = tv != null && tv.IsOn;
        DestroyVideos();
        if (tv == null || _gameChannels == null)
            return;
        Channels(tv) = _gameChannels;
        if (wasShowing)
            tv.Control(Mathf.Clamp(tv.displayedScreenModule, 0, _gameChannels.Length - 1));
    }

    private static void Rebuild()
    {
        var tv = Tv;
        if (!_active || tv == null)
            return;
        if (_gameChannels == null)
            _gameChannels = Channels(tv);

        bool wasOn = tv.IsOn;
        int shown = tv.displayedScreenModule;
        if (wasOn)
            DisableAllChannels(tv);
        DestroyVideos();

        var list = new List<ScreenModule>();
        var replaced = new HashSet<Station>();
        for (int i = 0; i < _gameChannels.Length; i++)
        {
            var channel = _gameChannels[i];
            var station = ReplacementFor(channel, i);
            if (station != null)
            {
                replaced.Add(station);
                list.Add(AddVideo(tv, station));
            }
            else if (_keepGameChannels && !IsHidden(channel, i))
            {
                list.Add(channel);
            }
        }
        foreach (var station in StationList)
        {
            if (!replaced.Contains(station))
                list.Add(AddVideo(tv, station));
        }
        // The TV can't work without channels; the first game channel (static) is the fallback.
        if (list.Count == 0 && _gameChannels.Length > 0)
            list.Add(_gameChannels[0]);

        Channels(tv) = list.ToArray();
        if (wasOn && list.Count > 0)
            tv.Control(Mathf.Clamp(shown, 0, list.Count - 1), noAudio: true);
    }

    private static ScreenModule AddVideo(TV tv, Station station)
    {
        var video = new VideoChannel(tv, _gameChannels[0], station);
        Videos.Add(video);
        return video.Module;
    }

    private static Station ReplacementFor(ScreenModule channel, int index)
    {
        foreach (var station in StationList)
        {
            if (station.Replaces != null && Matches(station.Replaces, channel, index))
                return station;
        }
        return null;
    }

    private static bool IsHidden(ScreenModule channel, int index)
    {
        foreach (string hidden in HiddenChannels)
        {
            if (Matches(hidden, channel, index))
                return true;
        }
        return false;
    }

    /// <summary>Game channels are named by their name ("Sports") or their number on the remote ("5").</summary>
    private static bool Matches(string reference, ScreenModule channel, int index) =>
        string.Equals(reference.Trim(), GameName(channel), StringComparison.OrdinalIgnoreCase) ||
        reference.Trim() == (index + 1).ToString();

    private static void DestroyVideos()
    {
        foreach (var video in Videos)
            video.Destroy();
        Videos.Clear();
    }
}
