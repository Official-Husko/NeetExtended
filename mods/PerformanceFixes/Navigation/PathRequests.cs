using System.Collections.Generic;
using PerformanceFixes.Common;
using UnityEngine;
using UnityEngine.AI;

namespace PerformanceFixes.Navigation;

/// <summary>
/// The characters' behaviour trees run their MoveTo node every frame, and each one calls
/// NavMeshAgent.SetDestination every time, which plans the whole path again even when the target
/// hasn't moved. This lets a request through only when the target moved, or when the agent has no
/// complete path to follow.
/// </summary>
internal static class PathRequests
{
    // Closer than this counts as the same target.
    private const float Tolerance = 0.1f;

    private static readonly Dictionary<NavMeshAgent, Vector3> LastTarget =
        new Dictionary<NavMeshAgent, Vector3>(ByReference<NavMeshAgent>.Instance);

    public static bool SetDestination(NavMeshAgent agent, Vector3 target)
    {
        if (LastTarget.TryGetValue(agent, out var last)
            && (last - target).sqrMagnitude < Tolerance * Tolerance
            && (agent.pathPending || (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete)))
            return true;

        LastTarget[agent] = target;
        return agent.SetDestination(target);
    }

    /// <summary>The agents are gone with their scene.</summary>
    public static void Forget() => LastTarget.Clear();
}
