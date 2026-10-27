using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace Coop.World.Hostiles;

/// <summary>
/// Host: cops on their way back to the van. The game's way out has no door handling (only its chase
/// bashes doors), so an agent leaving through a closed door can stand at it for good (or slide off
/// along a wall), and its van never drives off. A leaving agent that hasn't got closer to where it's
/// going for a moment bashes the door in front of it like a chasing one does; one still no closer a
/// few seconds later is put where it was walking to.
/// </summary>
internal static class CopExits
{
    private const float CheckEvery = 1f;
    private const float Progress = 0.3f;
    private const float BashAfter = 2f;
    private const float PlaceAfter = 8f;

    private struct Exit
    {
        public BrainAgentCop Cop;
        public float Closest;
        public float NextCheck;
        public float Stuck;
    }

    private static readonly List<Exit> Exits = new List<Exit>();

    private static readonly Func<BrainAgentCop, NodeState> DetectDoor =
        AccessTools.MethodDelegate<Func<BrainAgentCop, NodeState>>(AccessTools.Method(typeof(BrainAgentCop), "DetectNearbyDoor"));

    private static readonly Func<BrainAgentCop, NodeState> BashDoor =
        AccessTools.MethodDelegate<Func<BrainAgentCop, NodeState>>(AccessTools.Method(typeof(BrainAgentCop), "AttackNearbyDoor"));

    private static readonly AccessTools.FieldRef<BrainAgentCop, NavMeshAgent> Nav =
        AccessTools.FieldRefAccess<BrainAgentCop, NavMeshAgent>("nav");

    public static void Clear() => Exits.Clear();

    /// <summary>After a cop's Update.</summary>
    public static void AfterUpdate(BrainAgentCop cop)
    {
        if (!WorldSide.Running || !WorldSide.IsHost)
            return;
        bool leaving = cop.killCount > 0 || cop.leaveTimer <= 0f;
        int index = IndexOf(cop);
        float now = Time.time;
        if (!leaving)
        {
            if (index >= 0)
                Exits.RemoveAt(index);
            return;
        }
        if (cop.moveTgt == null)
            return;
        float left = Vector3.Distance(cop.transform.position, cop.moveTgt.Position);
        if (index < 0)
        {
            Exits.Add(new Exit { Cop = cop, Closest = left, NextCheck = now + CheckEvery });
            return;
        }
        var exit = Exits[index];
        if (now < exit.NextCheck)
            return;
        exit.NextCheck = now + CheckEvery;
        if (left < exit.Closest - Progress)
        {
            exit.Closest = left;
            exit.Stuck = 0f;
        }
        else
        {
            exit.Stuck += CheckEvery;
            if (exit.Stuck >= PlaceAfter)
            {
                var nav = Nav(cop);
                if (nav != null && nav.isOnNavMesh)
                    nav.Warp(cop.moveTgt.Position);
                exit.Stuck = 0f;
                exit.Closest = Vector3.Distance(cop.transform.position, cop.moveTgt.Position);
            }
            else if (exit.Stuck >= BashAfter && DetectDoor(cop) == NodeState.Success)
                BashDoor(cop);
        }
        Exits[index] = exit;
    }

    // A handful of cops at most; destroyed ones are dropped on the way.
    private static int IndexOf(BrainAgentCop cop)
    {
        for (int i = Exits.Count - 1; i >= 0; i--)
        {
            var other = Exits[i].Cop;
            if (other == null)
            {
                Exits.RemoveAt(i);
                continue;
            }
            if (other == cop)
                return i;
        }
        return -1;
    }
}
