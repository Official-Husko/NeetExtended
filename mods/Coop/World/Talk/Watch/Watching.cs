using System.Collections.Generic;
using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Talk.Watch;

/// <summary>
/// The conversations other players are in, shown in this game's dialogue box when this player is
/// close to the speaker and not shut off from it by a wall (<see cref="Earshot"/>, <see cref="WatchProtocol"/>).
/// Every game keeps the latest state of each conversation (the host feeds its own copy as it sends),
/// and the closest one in range is drawn from its current line: a player walking up mid-conversation starts there, silently.
///
/// Watching never takes over the watcher: the box is for show (its buttons can't be clicked) and no
/// speaker is set in <c>Dialogue</c>, so Use, movement and walking away work as usual. A
/// conversation of the watcher's own takes the box; the watched one comes back when it's over.
/// </summary>
internal static class Watching
{
    private const float EnterRange = 8f;
    private const float LeaveRange = 10f;
    private const float EnterHeight = 3.5f;
    private const float LeaveHeight = 4.5f;

    private static readonly Dictionary<long, WatchedTalk> Talks = new Dictionary<long, WatchedTalk>();

    private static WatchedTalk _shown;

    public static Dictionary<long, WatchedTalk>.ValueCollection All => Talks.Values;

    public static void Init() => Messages.On(MessageIds.WorldTalkWatch, OnMessage);

    public static void Clear()
    {
        if (_shown != null)
            TalkView.HideWatched();
        Talks.Clear();
        _shown = null;
        WatchVoice.Clear();
    }

    /// <summary>Every frame: who's close enough to watch, and walking up to or away from one.</summary>
    public static void Tick()
    {
        if (Talks.Count != 0 || _shown != null)
            Select(null);
    }

    /// <summary>Something else wrote in the box (a short line): the watched one is drawn again on the next frame.</summary>
    public static void Invalidate() => _shown = null;

    /// <summary>Dialogue.Save would store the box's text as the host's conversation: hide it while that runs.</summary>
    public static bool HideForSave()
    {
        var d = Dialogue._;
        if (_shown == null || d == null || d.storyText == null || !d.storyText.enabled || d.currSpeaker != null)
            return false;
        d.storyText.enabled = false;
        return true;
    }

    public static void ShowAfterSave()
    {
        var d = Dialogue._;
        if (d != null && d.storyText != null)
            d.storyText.enabled = true;
    }

    // The events, from the host's own hooks and from the network.

    public static void OnShow(SpeakerRef speaker, byte talker, bool playerVoice, int seed, string text)
    {
        if (!speaker.IsValid)
            return;
        long key = speaker.Key;
        if (!Talks.TryGetValue(key, out var t))
        {
            t = new WatchedTalk { Ref = speaker };
            Talks[key] = t;
        }
        t.NewLine(talker, text, playerVoice, seed);
        Select(t);
    }

    public static void OnChoices(SpeakerRef speaker, List<string> choices)
    {
        if (!Talks.TryGetValue(speaker.Key, out var t))
            return;
        t.Choices.Clear();
        t.Choices.AddRange(choices);
        t.ChoicesShown = true;
        t.Picked = -1;
        if (t == _shown)
            TalkView.ShowChoices(t.Choices, interactive: false);
    }

    public static void OnPick(SpeakerRef speaker, int index)
    {
        if (!Talks.TryGetValue(speaker.Key, out var t) || !t.ChoicesShown || index < 0 || index >= t.Choices.Count)
            return;
        t.Picked = index;
        if (t == _shown)
            TalkView.ShowPicked(t.Choices, index);
    }

    public static void OnClose(SpeakerRef speaker, bool keepText)
    {
        long key = speaker.Key;
        if (!Talks.TryGetValue(key, out var t))
            return;
        t.Choices.Clear();
        t.ChoicesShown = false;
        t.Picked = -1;
        if (keepText && t == _shown)
        {
            t.Lingering = true;
            TalkView.HideChoices();
            TalkVoice.Stop();
            return;
        }
        Talks.Remove(key);
        if (t == _shown)
        {
            _shown = null;
            TalkView.HideWatched();
        }
    }

    private static void OnMessage(in Sender from, NetDataReader r)
    {
        if (!WorldSide.Running || WorldSide.IsHost)
            return;
        byte kind = r.GetByte();
        var speaker = SpeakerRef.Read(r);
        switch (kind)
        {
            case WatchProtocol.Show:
                byte talker = r.GetByte();
                byte flags = r.GetByte();
                int seed = r.GetInt();
                OnShow(speaker, talker, (flags & WatchProtocol.PlayerVoice) != 0, seed, r.GetString());
                break;
            case WatchProtocol.Choices:
                int count = r.GetByte();
                var choices = new List<string>(count);
                for (int i = 0; i < count; i++)
                    choices.Add(r.GetString());
                OnChoices(speaker, choices);
                break;
            case WatchProtocol.Pick:
                OnPick(speaker, r.GetByte());
                break;
            case WatchProtocol.Close:
                OnClose(speaker, r.GetBool());
                break;
        }
    }

    // Decides what the box shows. "fresh" is a line that just arrived: it plays its voice if it's drawn.
    private static void Select(WatchedTalk fresh)
    {
        var d = Dialogue._;
        if (d == null || d.storyText == null)
            return;
        // This player's own conversation (or a short line) has the box.
        if (d.currSpeaker != null)
        {
            // The text is theirs now (the game wrote over it); only the watched choices are left to clear.
            if (_shown != null)
            {
                _shown = null;
                TalkView.HideChoices();
                TalkVoice.Stop();
            }
            return;
        }
        WatchedTalk best = null;
        WatchedTalk gone = null;
        var me = WorldSide.LocalPlayer;
        if (me != null)
        {
            var from = me.transform.position;
            foreach (var t in Talks.Values)
            {
                if (t.Talker == WorldSide.LocalSlot)
                    continue;
                if (t.Lingering && t != _shown)
                {
                    gone = t;
                    continue;
                }
                var speaker = t.Speaker;
                if (speaker == null)
                    continue;
                bool wasShown = t == _shown;
                if (!Near(from, speaker.transform.position, wasShown) || !t.Heard(from, speaker.transform.position))
                {
                    if (t.Lingering)
                        gone = t;
                    continue;
                }
                if (best == null || wasShown)
                    best = t;
            }
        }
        if (gone != null)
            Talks.Remove(gone.Ref.Key);
        if (best == _shown && (best == null || best != fresh))
            return;
        if (best != _shown && _shown != null)
            TalkView.HideWatched();
        _shown = best;
        if (best != null)
            Display(best, voice: best == fresh);
    }

    private static bool Near(Vector3 from, Vector3 speaker, bool wasShown)
    {
        float range = wasShown ? LeaveRange : EnterRange;
        float height = wasShown ? LeaveHeight : EnterHeight;
        var offset = speaker - from;
        return Mathf.Abs(offset.y) < height && offset.x * offset.x + offset.z * offset.z < range * range;
    }

    private static void Display(WatchedTalk t, bool voice)
    {
        TalkView.ShowWatched(t.Speaker, t.Text, t.PlayerVoice, voice ? Speakers.PlayerAt(t.Talker) : null, voice, t.Seed);
        if (!t.ChoicesShown)
            return;
        if (t.Picked >= 0)
            TalkView.ShowPicked(t.Choices, t.Picked);
        else
            TalkView.ShowChoices(t.Choices, interactive: false);
    }
}
