using System;
using System.Collections.Generic;
using NeetModKit.Game;
using UnityEngine;

namespace NeetModKit.Rules;

/// <summary>This game's own player, for rules (<see cref="RulePlayer.Local"/>).</summary>
internal sealed class LocalRulePlayer : IRulePlayer
{
    public static bool Exists => GameRefs.Player != null;

    private static GameObject Body => GameRefs.Player ?? throw new InvalidOperationException("No player.");

    public Vector3 Position => Body.transform.position;

    public Vector3 InFront(float distance) => Spawning.InFrontOfPlayer(distance);

    public float GetStat(string name) => PlayerControl.FindStat(name).GetVal();

    public void SetStat(string name, float value) => PlayerControl.FindStat(name).SetVal(value);

    public void AddStat(string name, float amount) => PlayerControl.FindStat(name).AddVal(amount);

    public void Knockout(float seconds) =>
        (GameRefs.PlayerStats ?? throw new InvalidOperationException("No player.")).knockout.AddVal(seconds);

    public bool Holds(ICollection<string> names)
    {
        var abilities = GameRefs.PlayerAbilities;
        if (abilities == null)
            return false;
        foreach (var held in abilities.pickups)
        {
            if (held != null && names.Contains(Spawning.PrefabName(held.name)))
                return true;
        }
        return false;
    }
}
