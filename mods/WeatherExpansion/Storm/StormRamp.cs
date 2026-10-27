using NeetLoader.Runtime;
using NeetModKit.Game;
using UnityEngine;

namespace WeatherExpansion.Storm;

/// <summary>
/// Moves the rain and the storm loop from one weather's level to the next instead of jumping. It owns
/// the two kit multipliers on behalf of one feature (its factors go away with that feature) and
/// only touches them five times a second while a change is under way.
/// </summary>
internal sealed class StormRamp
{
    private const float Step = 0.2f;

    private readonly ITracker _owner;
    private StormLevel _from;
    private StormLevel _to;
    private StormLevel _now;
    private float _startedAt;
    private float _length;
    private float _nextStep;
    private bool _moving;

    public StormRamp(ITracker owner, StormLevel start)
    {
        _owner = owner;
        _from = _to = _now = start;
        Apply();
    }

    public StormLevel Now => _now;

    public bool Moving => _moving;

    /// <summary>Heads for <paramref name="target"/> over <paramref name="seconds"/>, from wherever it is now.</summary>
    public void GoTo(StormLevel target, float seconds)
    {
        if (seconds <= 0.05f)
        {
            Jump(target);
            return;
        }
        _from = _now;
        _to = target;
        _startedAt = Time.time;
        _length = seconds;
        _nextStep = 0f;
        _moving = true;
    }

    public void Jump(StormLevel target)
    {
        _from = _to = _now = target;
        _moving = false;
        Apply();
    }

    public void Tick()
    {
        if (!_moving || Time.time < _nextStep)
            return;
        _nextStep = Time.time + Step;
        float t = Mathf.Clamp01((Time.time - _startedAt) / _length);
        float smooth = t * t * (3f - 2f * t);
        _now = StormLevel.Lerp(_from, _to, smooth);
        if (t >= 1f)
        {
            _now = _to;
            _moving = false;
        }
        Apply();
    }

    private void Apply()
    {
        Sky.RainAmount.Set(_owner, _now.Rain);
        Sky.StormVolume.Set(_owner, _now.Volume);
    }
}
