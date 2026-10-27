using System.Collections.Generic;
using UnityEngine;

namespace NeetModKit.Rules;

/// <summary>
/// A player that rules look at and act on: the <c>stat</c>, <c>holding</c> and <c>room</c>
/// conditions and the <c>stat</c>, <c>knockout</c>, <c>give</c> and <c>fire</c> actions.
/// <see cref="RulePlayer.Current"/> says which player that is right now.
/// </summary>
public interface IRulePlayer
{
    /// <summary>Where the player's body is.</summary>
    Vector3 Position { get; }

    /// <summary>A spot about <paramref name="distance"/> in front of the player, where items pop out for them.</summary>
    Vector3 InFront(float distance);

    /// <summary>A stat by its short name (Hunger, Health, Soul, ...). Throws for a name that isn't a stat.</summary>
    float GetStat(string name);

    /// <summary>Sets a stat through the game's setter, so thresholds and the HUD react as usual.</summary>
    void SetStat(string name, float value);

    void AddStat(string name, float amount);

    /// <summary>Knocks the player out for <paramref name="seconds"/> more.</summary>
    void Knockout(float seconds);

    /// <summary>Whether either hand holds an item whose prefab name is in <paramref name="names"/>.</summary>
    bool Holds(ICollection<string> names);
}
