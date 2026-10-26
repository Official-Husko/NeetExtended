using System;
using UnityEngine;

namespace PumpkinSmash.Game;

/// <summary>
/// The game rules, with no UI: a pumpkin pops up in one of nine holes, smash it before it hides.
/// It stays up a little shorter the more you score. Missing (hitting an empty hole) costs a point.
/// </summary>
public sealed class SmashRound
{
    public const int Holes = 9;
    private const float SlowestUp = 1.1f;
    private const float FastestUp = 0.45f;

    private float _upFor;

    public bool Running { get; private set; }
    public int Score { get; private set; }
    public float TimeLeft { get; private set; }

    /// <summary>Hole with the pumpkin, or -1.</summary>
    public int Pumpkin { get; private set; } = -1;

    public event Action Changed;
    public event Action<int> Finished;

    public void Start(float seconds)
    {
        Score = 0;
        TimeLeft = seconds;
        Running = true;
        PopUp();
    }

    public void Tick(float dt)
    {
        if (!Running)
            return;

        TimeLeft -= dt;
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            Running = false;
            Pumpkin = -1;
            Changed?.Invoke();
            Finished?.Invoke(Score);
            return;
        }

        _upFor -= dt;
        if (_upFor <= 0f)
            PopUp();
        Changed?.Invoke();
    }

    public void Hit(int hole)
    {
        if (!Running)
            return;
        if (hole == Pumpkin)
        {
            Score++;
            PopUp();
        }
        else
        {
            Score = Mathf.Max(0, Score - 1);
        }
        Changed?.Invoke();
    }

    private void PopUp()
    {
        int next;
        do { next = UnityEngine.Random.Range(0, Holes); } while (next == Pumpkin);
        Pumpkin = next;
        _upFor = Mathf.Lerp(SlowestUp, FastestUp, Mathf.Clamp01(Score / 40f));
    }
}
