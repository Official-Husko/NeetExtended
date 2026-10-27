using UnityEngine;

namespace Coop.World.Hostiles;

/// <summary>
/// Host: one hitman job at a time (see <see cref="Contracts.SendHitman"/>). After the delay the cop
/// van comes (the game's own, so it looks and leaves like any raid), its agents get out within a few
/// seconds and go after the target only, wherever they are. A target who's down, dead or gone ends
/// the job: the agents walk back to the van and drive off, like after any kill.
/// </summary>
internal static class Hitman
{
    // Agents get out this soon after the van parks (the game waits 30-120 s for a raid).
    private const float GetOutAfter = 5f;
    /// <summary>Long enough to cross the house; a raid's agents give up after 120-240 s.</summary>
    public const float AgentPatience = 600f;

    private enum Phase { None, Waiting, Coming }

    private static Phase _phase;
    private static byte _target = WorldSide.None;
    private static float _due;
    private static GameObject _van;
    private static bool _calledOff;

    public static bool Busy => _phase != Phase.None;

    public static byte Target => _phase != Phase.None ? _target : WorldSide.None;

    public static bool Send(byte slot, float delaySeconds)
    {
        if (!WorldSide.Running || !WorldSide.IsHost || Busy || Targets.BodyOf(slot) == null)
            return false;
        _target = slot;
        _due = Time.time + Mathf.Max(0f, delaySeconds);
        _phase = Phase.Waiting;
        _van = null;
        _calledOff = false;
        CoopMod.Log.Info($"World: a hitman for {Talk.Speakers.NameOf(slot)} in {Mathf.Max(0f, delaySeconds):0} s.");
        return true;
    }

    public static void Clear()
    {
        _phase = Phase.None;
        _target = WorldSide.None;
        _van = null;
    }

    /// <summary>True when <paramref name="cop"/> works for the job, with the target's slot.</summary>
    public static bool Hunts(BrainAgentCop cop, out byte slot)
    {
        slot = _target;
        return _phase == Phase.Coming && _van != null && cop.van == _van;
    }

    public static bool IsJobVan(CopVan van) => _phase == Phase.Coming && _van != null && van.gameObject == _van;

    /// <summary>After the job's van started: its agents get out soon.</summary>
    public static void VanStarted(CopVan van)
    {
        if (IsJobVan(van))
            van.spawnTimer = GetOutAfter;
    }

    /// <summary>Host, every frame.</summary>
    public static void Tick()
    {
        if (_phase == Phase.None)
            return;
        // Gone means out of the session: a body that's rebuilt for a moment (new clothes, a reload) isn't.
        bool targetHere = InGame(_target);
        if (_phase == Phase.Waiting)
        {
            if (!targetHere)
            {
                CoopMod.Log.Info("World: the hitman's target left; the job is off.");
                Clear();
                return;
            }
            // One van at a time: a raid already in the street goes first.
            if (Time.time < _due || HouseManager._ == null || HouseManager._.copVan != null)
                return;
            _van = HouseManager._.SpawnCopVan();
            _phase = _van != null ? Phase.Coming : Phase.Waiting;
            return;
        }
        if (_van == null)
        {
            Clear(); // The agents went home and the van left.
            return;
        }
        if (!targetHere && !_calledOff)
            CallOff();
    }

    /// <summary>True while <paramref name="slot"/>'s player is in the session and in the house.</summary>
    public static bool InGame(byte slot)
    {
        foreach (var player in Session.CoopSession.Players)
        {
            if (player.Slot == slot)
                return player.IsLocal || player.InWorld;
        }
        return slot == WorldSide.LocalSlot;
    }

    // Everyone back to the van, the way the game sends them after a kill.
    public static void CallOff()
    {
        if (_van == null || !_van.TryGetComponent(out CopVan van))
            return;
        _calledOff = true;
        van.InformAgentsOfKill(1);
    }
}
