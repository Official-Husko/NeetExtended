using System;
using System.Collections.Generic;
using NeetLoader.Runtime;

namespace NeetModKit.Rules;

/// <summary>
/// Which player rules are about right now: normally this game's own player. A mod that runs a
/// conversation for someone else (co-op runs remote players' conversations in the host's game)
/// adds a source that names that player while it runs:
/// <code>
/// RulePlayer.AddSource(this, () => talking != null ? talking.Player : null);
/// </code>
/// </summary>
public static class RulePlayer
{
    private static readonly List<Func<IRulePlayer>> Sources = new List<Func<IRulePlayer>>();

    /// <summary>This game's own player. Its members throw while there's no player.</summary>
    public static IRulePlayer Local { get; } = new LocalRulePlayer();

    /// <summary>
    /// The newest source's answer, else this game's player; null when there's no player at all
    /// (outside the house).
    /// </summary>
    public static IRulePlayer Current
    {
        get
        {
            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                var player = Sources[i]();
                if (player != null)
                    return player;
            }
            return LocalRulePlayer.Exists ? Local : null;
        }
    }

    /// <summary><see cref="Current"/>, or an error when there's no player.</summary>
    public static IRulePlayer Require() => Current ?? throw new InvalidOperationException("No player.");

    /// <summary>
    /// Adds a source: it returns the player rules are about, or null to leave it to the others.
    /// It's asked every time a rule needs the player, so keep it cheap. Removed when the owner cleans up.
    /// </summary>
    public static void AddSource(ITracker owner, Func<IRulePlayer> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        Sources.Add(source);
        owner.Track(Disposable.Create(() => Sources.Remove(source)));
    }
}
