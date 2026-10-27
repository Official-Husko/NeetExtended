using NeetLoader.Runtime;
using NeetModKit.Events;
using NeetModKit.Features;
using NeetModKit.Game;
using NeetModKit.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeatherExpansion.Storm;

/// <summary>
/// The storm changes over the night. The host moves the weather between drizzle, rain and storm (with
/// a strength) on a schedule: each spell lasts a few minutes, the next one is planned when a spell
/// starts and published as the forecast, and the rain, the storm loop and the wait between lightning
/// strikes ease from one spell's level to the next. It never clears up.
///
/// A game that shows someone else's house (a co-op client) doesn't schedule anything: it takes the
/// weather the host set (<see cref="Sky.Weather"/>) and eases its own rain and wind to match.
/// Another mod setting the weather (a cheat) is taken as the new spell and the schedule goes on from there.
/// The state is saved with the house (<see cref="StormMemory"/>).
/// </summary>
internal sealed class StormScheduleFeature : ModFeature
{
    public const string FeatureId = "StormSchedule";

    // The change a cheat or a co-op host's new weather gets: quick, so it shows.
    private const float FollowRamp = 12f;
    private const float AdoptRamp = 4f;
    private const float LimitRamp = 8f;

    private StormRamp _ramp;
    private StormPhase _phase;
    private StormPhase _next;
    private float _startedAt;
    private float _nextAt;
    private float _rescheduleAt;
    private float _rampEndsAt;
    private float _checkAt;
    private bool _running;
    private bool _drives;
    private bool _publishing;
    private bool _republish;
    private WeatherState? _adopt;

    public override string Id => FeatureId;
    public override string Name => "Changing weather";
    public override string Description =>
        "Experimental. The storm changes over the night: it moves between drizzle, rain and a full storm, lighter and heavier, " +
        "and never clears up. The rain, the wind and how often lightning strikes follow it. How fast, how strong and how stormy are in Settings.";

    protected override void OnEnable()
    {
        _ramp = new StormRamp(this, StormLevels.Of(WeatherKind.Rain, 1f));
        _running = false;
        _adopt = null;
        _republish = false;
        Sky.WeatherChanged.Subscribe(this, OnWeatherChanged);
        Sky.SetThunderDelay(this, ThunderDelay);
        StormMemory.Restored += OnRestored;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        WeatherExpansionMod.Config.Changed += OnSettingsChanged;
        Track(Disposable.Create(() =>
        {
            StormMemory.Restored -= OnRestored;
            WeatherExpansionMod.Config.Changed -= OnSettingsChanged;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }));
    }

    protected override void OnDisable()
    {
        _running = false;
        _ramp = null;
    }

    public override void OnUpdate()
    {
        if (!GameEvents.IsHouseReady)
            return;
        bool drives = WorldRole.RunsWorld;
        if (!_running || drives != _drives)
            Start(drives);
        _ramp.Tick();
        if (!_drives)
            return;

        float now = ModFlags.PlaySeconds;
        if (Time.time >= _checkAt)
        {
            _checkAt = Time.time + 0.5f;
            var config = WeatherExpansionMod.Config.Value;
            if (_adopt.HasValue)
            {
                var asked = _adopt.Value;
                _adopt = null;
                BeginPhase(new StormPhase(asked.Kind, ClampIntensity(asked.Intensity)), now, config, AdoptRamp);
            }
            else if (_republish)
            {
                _republish = false;
                Publish();
            }
            else if (_phase.Kind > config.StrongestWeather)
            {
                // The limit was lowered below what is going on: ease down a step.
                BeginPhase(StormPlan.Next(_phase, config.StrongestWeather, config.Storminess), now, config, LimitRamp);
            }
            else if (now >= _nextAt)
            {
                BeginPhase(_next, now, config, -1f);
            }
            else if (_next.Kind > config.StrongestWeather)
            {
                _next = StormPlan.Next(_phase, config.StrongestWeather, config.Storminess);
                Publish();
                Remember();
            }
        }
        if (_rescheduleAt > 0f && Time.time >= _rescheduleAt)
            Reschedule();
    }

    // ---- start, save and load ----

    private void Start(bool drives)
    {
        _running = true;
        _drives = drives;
        _rescheduleAt = 0f;
        if (!drives)
        {
            _ramp.Jump(StormLevels.Of(Sky.Weather.Kind, Sky.Weather.Intensity));
            return;
        }

        var config = WeatherExpansionMod.Config.Value;
        float now = ModFlags.PlaySeconds;
        var saved = StormMemory.TakeSaved();
        if (saved != null)
        {
            _phase = new StormPhase((WeatherKind)saved.Kind, ClampIntensity(saved.Intensity));
            _next = new StormPhase((WeatherKind)saved.NextKind, ClampIntensity(saved.NextIntensity));
            _startedAt = now - saved.Age;
            _nextAt = now + saved.Left;
        }
        else if (!Sky.Weather.Equals(WeatherState.Default))
        {
            // Someone else was already running the weather (a co-op client that is hosting now): carry on from it.
            BeginPhase(new StormPhase(Sky.Weather.Kind, ClampIntensity(Sky.Weather.Intensity)), now, config, 0f);
            return;
        }
        else
        {
            // A new game: the game's own steady rain, then the night starts to move.
            BeginPhase(new StormPhase(WeatherKind.Rain, 1f), now, config, 0f);
            return;
        }
        _ramp.Jump(StormLevels.Of(_phase.Kind, _phase.Intensity));
        Publish();
        Remember();
        _rescheduleAt = Time.time + 1f;
    }

    private void OnRestored()
    {
        // A save was loaded or this mod came back from a reload: start over from what it holds.
        _running = false;
    }

    private void OnSceneUnloaded(Scene scene) => _running = false;

    // The thunder setting counts for the strike that is being waited for too.
    private void OnSettingsChanged(WeatherExpansionConfig config)
    {
        if (_running && _drives && _rescheduleAt <= 0f)
            _rescheduleAt = Time.time + 0.1f;
    }

    // ---- moving the weather ----

    /// <param name="ramp">Seconds to ease into it; 0 puts the scene there at once, below 0 picks a length that fits the spell.</param>
    private void BeginPhase(StormPhase phase, float now, WeatherExpansionConfig config, float ramp)
    {
        _phase = phase;
        _startedAt = now;
        float length = StormPlan.Duration(config.WeatherMinutes);
        if (ramp < 0f)
            ramp = StormPlan.RampFor(length);
        _nextAt = now + length;
        _next = StormPlan.Next(phase, config.StrongestWeather, config.Storminess);
        var level = StormLevels.Of(phase.Kind, phase.Intensity);
        if (ramp <= 0f)
            _ramp.Jump(level);
        else
            _ramp.GoTo(level, ramp);
        Publish();
        Remember();
        if (ramp > 0f)
        {
            // The wait for lightning is asked for again halfway through the change and at its end, so a storm
            // that is arriving doesn't sit out a drizzle's long wait.
            _rampEndsAt = Time.time + ramp;
            _rescheduleAt = Time.time + ramp * 0.5f;
        }
        else
        {
            _rescheduleAt = Time.time + 1f;
        }
    }

    private void Reschedule()
    {
        Sky.Reschedule();
        if (_rampEndsAt > 0f && Time.time < _rampEndsAt - 0.5f)
        {
            _rescheduleAt = _rampEndsAt;
            return;
        }
        _rescheduleAt = 0f;
        _rampEndsAt = 0f;
    }

    private void Publish()
    {
        _publishing = true;
        try
        {
            Sky.SetWeather(this, new WeatherState(_phase.Kind, _phase.Intensity, _startedAt, _next.Kind, _nextAt));
        }
        finally
        {
            _publishing = false;
        }
    }

    private void Remember() => StormMemory.Record(_phase, _startedAt, _next, _nextAt);

    private void OnWeatherChanged(WeatherState state)
    {
        if (_publishing || !_running || _ramp == null)
            return;
        if (!_drives)
        {
            _ramp.GoTo(StormLevels.Of(state.Kind, state.Intensity), FollowRamp);
            return;
        }
        // Whoever had the weather let go of it: put ours back on the next check.
        if (state.Equals(WeatherState.Default))
        {
            _republish = true;
            return;
        }
        // Another mod set the weather: that is the new spell, and the schedule goes on from here.
        // Done on the next check, not inside the other mod's call, so the state events stay in order.
        _adopt = state;
    }

    private float ThunderDelay(float min, float max)
    {
        var level = _ramp != null ? _ramp.Now : StormLevels.Of(WeatherKind.Rain, 1f);
        float frequency = Mathf.Max(0.1f, WeatherExpansionMod.Config.Value.ThunderFrequency);
        return Random.Range(level.ThunderMin, level.ThunderMax) / frequency;
    }

    private static float ClampIntensity(float intensity) =>
        Mathf.Clamp(intensity, StormLevels.LowestAccepted, StormLevels.HighestAccepted);
}
