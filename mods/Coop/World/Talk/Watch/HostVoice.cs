using System.Collections;
using UnityEngine;

namespace Coop.World.Talk.Watch;

/// <summary>
/// <c>Dialogue.PlayVoiceAudio</c> for the host's own conversations, with the dice rolled from the
/// line's seed (<see cref="LineVoice"/>) so the host's box sounds like what everyone watching hears.
/// Same steps as the game's.
/// </summary>
internal static class HostVoice
{
    /// <summary>For tests: blips the host's own box played, and the last pitch.</summary>
    public static int Blips { get; private set; }

    public static string LastPitch { get; private set; } = "";

    public static IEnumerator Play(Dialogue d, int seed)
    {
        // A copy per line: two lines can overlap for a moment.
        var roll = new LineVoice();
        roll.Reseed(seed);
        var speaker = d.currSpeaker;
        bool oneShot = speaker.soundOneShot;
        if (oneShot)
        {
            var sources = speaker.GetComponents<AudioSource>();
            if (sources.Length > 0)
                d.currentAudio = sources[roll.Source(sources.Length)];
        }
        int blips = roll.Blips(oneShot, d.storyText.text);
        for (int i = 0; i < blips; i++)
        {
            if (d.currSpeaker == null)
                yield break;
            float pitch = roll.Pitch();
            float wait = roll.Wait();
            if (d.currentAudio != null)
            {
                if (!oneShot)
                    d.currentAudio.pitch = pitch;
                d.currentAudio.Play();
                Blips++;
                LastPitch = pitch.ToString("0.000");
            }
            yield return new WaitForSeconds(wait);
        }
        if (d.currSpeaker == null)
            d.currentAudio = null;
    }
}
