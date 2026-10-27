using System;
using System.Collections.Generic;

namespace Randomizer.Seeds;

/// <summary>
/// Random numbers from the save's seed, one stream per thing being randomized, so the same seed
/// always gives the same visitors, prices and house, and one part never shifts another. The game's
/// own UnityEngine.Random is left alone.
/// </summary>
public static class Rolls
{
    /// <summary>A generator for <paramref name="what"/> ("visitors", "price:Mug", ...) under <paramref name="seed"/>.</summary>
    public static System.Random For(int seed, string what) => new System.Random(Hash(seed, what));

    /// <summary>A number from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public static double Between(this System.Random random, double min, double max) => min + random.NextDouble() * (max - min);

    /// <summary>Shuffles a list in place (Fisher-Yates).</summary>
    public static void Shuffle<T>(this System.Random random, IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>A seed from text: numbers are used as they are, anything else is hashed.</summary>
    public static int FromText(string text)
    {
        text = text?.Trim() ?? "";
        return int.TryParse(text, out int number) ? number : Hash(0, text);
    }

    // FNV-1a: string.GetHashCode differs between runs, this doesn't.
    private static int Hash(int seed, string text)
    {
        unchecked
        {
            uint hash = 2166136261u ^ (uint)seed;
            hash *= 16777619u;
            foreach (char c in text ?? "")
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return (int)hash;
        }
    }
}
