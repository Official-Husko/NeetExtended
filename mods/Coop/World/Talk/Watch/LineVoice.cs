using System;
using UnityEngine;
using Random = System.Random;

namespace Coop.World.Talk.Watch;

/// <summary>
/// The dice behind a line's voice blips, rolled from a seed instead of <c>UnityEngine.Random</c> so
/// every game that plays a line rolls the same ones: how many blips, which source (a speaker that
/// plays a random one), each blip's pitch and the wait after it. The game that runs the line picks
/// the seed and sends it with the line; the numbers are <c>Dialogue.PlayVoiceAudio</c>'s own (one
/// blip, plus 0 or 1, plus about one per two words; pitch 0.8 to 1.4; 0.1 to 0.2 s apart).
/// The global random state is never touched.
/// </summary>
internal sealed class LineVoice
{
    private static readonly Random Seeds = new Random(Environment.TickCount);

    private Random _rng = new Random(0);

    public static int NewSeed() => Seeds.Next();

    public void Reseed(int seed) => _rng = new Random(seed);

    /// <summary>How many blips a line plays: one for a speaker with one-shot sounds.</summary>
    public int Blips(bool oneShot, string text) =>
        oneShot ? 1 : 1 + _rng.Next(0, 2) + Mathf.Max(0, Mathf.RoundToInt(Words(text) * 0.42f));

    /// <summary>Which of a one-shot speaker's sources plays.</summary>
    public int Source(int count) => _rng.Next(count);

    public float Pitch() => 0.8f + 0.6f * (float)_rng.NextDouble();

    public float Wait() => 0.1f + 0.1f * (float)_rng.NextDouble();

    public static int Words(string text)
    {
        int words = 1;
        foreach (char c in text)
        {
            if (c == ' ')
                words++;
        }
        return words;
    }
}
