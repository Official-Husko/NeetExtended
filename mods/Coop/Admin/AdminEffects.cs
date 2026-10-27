using System.Collections;
using Coop.Players;
using Coop.Session;
using NeetModKit.Game;
using UnityEngine;

namespace Coop.Admin;

/// <summary>
/// What the host's Players tab does to this game's own player: run here, on the real body, so it
/// shows for everyone the way anything the player does shows.
/// </summary>
internal static class AdminEffects
{
    private static readonly string[] Needs = { "Health", "Hunger", "Bladder", "Hygiene", "Energy" };
    private const float SlapSeconds = 5f;
    private const float FreezeSeconds = 8f;
    private const float YeetSpeed = 11f;

    private static int _freezes;
    private static float _speedBeforeFreeze = -1f;

    public static void Apply(AdminAction action, Vector3 point)
    {
        var player = GameRefs.Player;
        if (player == null)
            return;
        var stats = player.GetComponent<PlayerStats>();
        var rig = player.GetComponent<Player_Abilities>();
        switch (action)
        {
            case AdminAction.Heal:
                foreach (string need in Needs)
                    PlayerControl.SetStat(need, 100f);
                if (stats != null && stats.knockout != null && stats.knockout.GetVal() > 0f)
                    stats.knockout.SetVal(0f);
                if (Downed.LocalDown && CoopSession.Local != null)
                    Downed.Revive(CoopSession.Local.Slot);
                break;
            case AdminAction.BringHere:
                PlayerControl.Teleport(point);
                break;
            case AdminAction.Slap:
                if (stats != null && stats.knockout != null)
                    stats.knockout.AddVal(SlapSeconds);
                break;
            case AdminAction.Yeet:
                if (player.TryGetComponent(out Rigidbody body))
                {
                    var sideways = Random.insideUnitCircle * 3f;
                    body.AddForce(new Vector3(sideways.x, YeetSpeed, sideways.y), ForceMode.VelocityChange);
                }
                break;
            case AdminAction.Spin:
                PlayerControl.Look(-player.transform.forward);
                break;
            case AdminAction.Freeze:
                Freeze();
                break;
            case AdminAction.Starve:
                PlayerControl.SetStat("Hunger", 3f);
                break;
            case AdminAction.PissAlarm:
                PlayerControl.SetStat("Bladder", 2f);
                break;
            case AdminAction.Shit:
                if (rig != null && !rig.dead)
                    rig.Shit();
                break;
            case AdminAction.KnockDown:
                // In co-op dying takes the player down first; anyone can still get them back up.
                if (rig != null && !rig.dead && !Downed.LocalDown)
                    rig.DieAccessor();
                break;
        }
    }

    // Barely moving for a few seconds, then back to whatever speed they had. Freezing again starts the time over.
    private static void Freeze()
    {
        if (CoopMod.Instance == null)
            return;
        if (_speedBeforeFreeze < 0f)
            _speedBeforeFreeze = PlayerControl.SpeedMultiplier;
        PlayerControl.SetSpeedMultiplier(0.1f);
        CoopMod.Instance.StartCoroutine(Thaw(++_freezes));
    }

    private static IEnumerator Thaw(int freeze)
    {
        yield return new WaitForSeconds(FreezeSeconds);
        if (freeze == _freezes)
            Unfreeze();
    }

    /// <summary>The mod unloads or the session ends: nobody stays frozen.</summary>
    public static void Unfreeze()
    {
        if (_speedBeforeFreeze < 0f)
            return;
        PlayerControl.SetSpeedMultiplier(_speedBeforeFreeze);
        _speedBeforeFreeze = -1f;
    }
}
