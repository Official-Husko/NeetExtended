using Coop.World.Talk.Watch;
using UnityEngine;

namespace Coop.World.Talk;

/// <summary>
/// A line's voice blips (<c>Dialogue.PlayVoiceAudio</c>) for a box driven from outside the story,
/// played from <see cref="Tick"/> instead of a coroutine. The dice come from the line's seed
/// (<see cref="LineVoice"/>), so the talker's box and everyone watching play the same blips.
/// </summary>
internal static class TalkVoice
{
    private static readonly LineVoice Roll = new LineVoice();

    private static AudioSource _voice;
    private static bool _oneShot;
    private static int _blipsLeft;
    private static float _nextBlip;

    /// <summary>For tests: how many blips played in this game, and the last one's source and the pitch.</summary>
    public static int Blips { get; private set; }

    public static string LastVoice { get; private set; } = "";

    public static void Clear()
    {
        _voice = null;
        _blipsLeft = 0;
    }

    public static void Stop() => _blipsLeft = 0;

    /// <summary>
    /// A line's blips start. The speaker's own voice, or the player's: the dialogue box's flat sound,
    /// or (with a <paramref name="talker"/>, for a watcher) one from the talker's body.
    /// </summary>
    public static void Begin(Dialogue d, CharacterStories speaker, string text, bool playerVoice, Transform talker, int seed)
    {
        Roll.Reseed(seed);
        _oneShot = speaker != null && speaker.soundOneShot;
        if (_oneShot)
        {
            var sources = speaker.GetComponents<AudioSource>();
            _voice = sources.Length > 0 ? sources[Roll.Source(sources.Length)] : null;
        }
        else
            _voice = playerVoice || speaker == null ? PlayerVoice(d, talker) : SpeakerVoice(d, speaker, talker);
        _blipsLeft = Roll.Blips(_oneShot, text);
        _nextBlip = Time.time;
    }

    public static void Tick()
    {
        if (_blipsLeft <= 0)
            return;
        float now = Time.time;
        if (now < _nextBlip)
            return;
        float pitch = Roll.Pitch();
        float wait = Roll.Wait();
        if (_voice != null)
        {
            if (!_oneShot)
                _voice.pitch = pitch;
            _voice.Play();
            LastVoice = _voice.name + " at " + _voice.transform.position.ToString("0.0") + " pitch " + pitch.ToString("0.000");
            Blips++;
        }
        _blipsLeft--;
        _nextBlip = now + wait;
    }

    private static AudioSource SpeakerVoice(Dialogue d, CharacterStories speaker, Transform talker)
    {
        if (speaker.TryGetComponent(out AudioSource own) && own.clip != null)
        {
            if (speaker.dialogueAudio != null)
                own.clip = speaker.dialogueAudio;
            return own;
        }
        return PlayerVoice(d, talker);
    }

    private static AudioSource PlayerVoice(Dialogue d, Transform talker)
    {
        if (talker != null)
        {
            var world = WatchVoice.At(d, talker.position + Vector3.up * 1.5f);
            if (world != null)
                return world;
        }
        return d.TryGetComponent(out AudioSource voice) ? voice : null;
    }
}
