using System.Collections.Generic;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// The body animator's bool parameters (isWalking, isCrouching, isSitting, ...) packed into a mask.
/// Every player rig uses the same controller, so both sides list them in the same order.
/// </summary>
internal sealed class AnimatorBools
{
    private readonly int[] _hashes;

    public AnimatorBools(Animator animator)
    {
        var hashes = new List<int>();
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && hashes.Count < 16)
                hashes.Add(parameter.nameHash);
        }
        _hashes = hashes.ToArray();
    }

    public ushort Read(Animator animator)
    {
        int mask = 0;
        for (int i = 0; i < _hashes.Length; i++)
        {
            if (animator.GetBool(_hashes[i]))
                mask |= 1 << i;
        }
        return (ushort)mask;
    }

    public void Apply(Animator animator, ushort mask)
    {
        for (int i = 0; i < _hashes.Length; i++)
            animator.SetBool(_hashes[i], (mask & (1 << i)) != 0);
    }
}
