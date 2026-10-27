using System.Collections.Generic;
using HarmonyLib;
using NeetModKit.Game;
using Mixtapes.Audio;
using Mixtapes.Tapes;
using UnityEngine;

namespace Mixtapes.Radio;

/// <summary>
/// With no tape in, a radio that's switched on and has power plays the mixtapes one after another,
/// shuffled, like a station. A tape going in takes over, as always.
/// </summary>
internal static class RadioStation
{
    private static readonly AccessTools.FieldRef<global::Radio, bool> On = AccessTools.FieldRefAccess<global::Radio, bool>("on");
    private static readonly List<AudioClip> Playlist = new List<AudioClip>();
    private static readonly HashSet<global::Radio> Playing = new HashSet<global::Radio>();
    private static int _last = -1;

    public static bool Enabled { get; set; }

    /// <summary>Builds the playlist from the tapes whose songs have loaded.</summary>
    public static void Refresh()
    {
        Playlist.Clear();
        foreach (var tape in TapeLoader.Tapes)
        {
            var clip = SongLibrary.Loaded(tape.File);
            if (clip != null && !Playlist.Contains(clip))
                Playlist.Add(clip);
        }
    }

    /// <summary>
    /// Every frame for every radio (from Radio.Update). Not in someone else's co-op house: there the
    /// host's station picks the songs and co-op tells this game's radios what to play.
    /// </summary>
    public static void Tick(global::Radio radio)
    {
        if (!Enabled || Playlist.Count == 0 || radio.cassette != null || radio.musicAudio == null || !WorldRole.RunsWorld)
            return;
        var speaker = radio.musicAudio;
        if (speaker.isPlaying || !On(radio) || !radio.Powered)
            return;
        speaker.loop = false;
        speaker.clip = Next();
        speaker.Play();
        Playing.Add(radio);
    }

    /// <summary>A tape is going in: the station stops and the speaker loops the tape again, as the game expects.</summary>
    public static void TapeIn(global::Radio radio)
    {
        if (!Playing.Remove(radio) || radio.musicAudio == null)
            return;
        radio.musicAudio.Stop();
        radio.musicAudio.loop = true;
    }

    /// <summary>Switched off or unloading: stations stop and the speakers are put back.</summary>
    public static void StopAll()
    {
        foreach (var radio in Playing)
        {
            if (radio == null || radio.musicAudio == null)
                continue;
            if (radio.cassette == null)
            {
                radio.musicAudio.Stop();
                radio.musicAudio.clip = null;
            }
            radio.musicAudio.loop = true;
        }
        Playing.Clear();
    }

    private static AudioClip Next()
    {
        int next;
        if (Playlist.Count == 1)
            next = 0;
        else if (_last < 0 || _last >= Playlist.Count)
            next = Random.Range(0, Playlist.Count);
        else
        {
            next = Random.Range(0, Playlist.Count - 1);
            if (next >= _last)
                next++; // Never the same song twice in a row.
        }
        _last = next;
        return Playlist[next];
    }
}
