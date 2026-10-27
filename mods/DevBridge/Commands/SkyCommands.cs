using System.Collections;
using DevBridge.Server;
using NeetModKit.Game;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>The house's sky: lights, fog, rain, lightning, the thunder timer and the weather state (the framework's Sky).</summary>
internal static class SkyCommands
{
    private static int _strikes;
    private static float _lastStrike = -1f;

    /// <summary>Counts strikes so tests can compare how many came in some time.</summary>
    internal static void CountStrikes(DevBridgeMod owner) =>
        Sky.OnStrike.Subscribe(owner, () =>
        {
            _strikes++;
            _lastStrike = Time.time;
        });

    [BridgeCommand("sky.get", "Moon and sun, fog, ambient, skybox, thunder (loop, next strike), rain systems, storm volume and the weather state.")]
    private static object Get(JObject p) => new JObject
    {
        ["moon"] = Light(Sky.Moon, Sky.MoonBaseIntensity),
        ["sun"] = Light(Sky.Sun, -1f),
        ["fog"] = new JObject
        {
            ["enabled"] = RenderSettings.fog,
            ["mode"] = RenderSettings.fogMode.ToString(),
            ["density"] = RenderSettings.fogDensity,
            ["sceneDensity"] = Sky.SceneFogDensity,
            ["amount"] = Sky.FogAmount.Value,
            ["color"] = Color(RenderSettings.fogColor),
        },
        ["ambient"] = new JObject
        {
            ["mode"] = RenderSettings.ambientMode.ToString(),
            ["color"] = Color(RenderSettings.ambientLight),
            ["intensity"] = RenderSettings.ambientIntensity,
        },
        ["skybox"] = RenderSettings.skybox != null ? RenderSettings.skybox.name : null,
        ["thunder"] = Thunder(),
        ["rain"] = Rain(),
        ["stormVolume"] = StormVolume(),
        ["weather"] = Weather(),
    };

    [BridgeCommand("sky.strike", "Lightning now (Sky.Strike).",
        Params = new[]
        {
            "story?: true runs the story's own thunder sequence instead (it stops the random loop unless the ThunderLoop fix is on)",
            "measure?: wait for the strike to end and report the flash: seconds the moon was above 1, frames, peak, end intensity, how long a lamp stayed dipped",
        })]
    private static object Strike(JObject p)
    {
        if (p.Bool("story", false))
        {
            if (SequenceManager._ == null)
                throw new System.InvalidOperationException("No sequence manager (not in the house?).");
            SequenceManager._.TriggerSequence("Execute", "Thunder");
            return "story strike";
        }
        if (!p.Bool("measure", false))
            return Sky.Strike() ? "struck" : throw new System.InvalidOperationException("No house to strike here (a co-op client doesn't strike).");

        var result = new Deferred();
        DevBridgeMod.Instance.StartCoroutine(SkyMeasure.Strike(result));
        return result;
    }

    [BridgeCommand("sky.set", "Change the sky; each parameter is optional. The multipliers belong to the bridge until reset.",
        Params = new[]
        {
            "fog?: fog density, set directly", "moon?: the moon's intensity, set directly", "sun?: the sun's intensity (and switches it on when above 0)",
            "fogAmount?: fog multiplier", "rainAmount?: rain multiplier", "stormVolume?: storm loop volume multiplier",
            "weather?: drizzle, rain or storm", "intensity?: weather intensity, default 1", "next?: forecast kind, with nextIn?: seconds until it",
            "thunderMin?, thunderMax?: seconds between random strikes (both, or just one for a fixed wait); \"default\" for thunderMin gives the timer back",
            "strikeIn?: move the next random strike to this many seconds from now", "reschedule?: true asks the delay provider for a new wait",
            "reset?: true clears every multiplier, the thunder delay and the weather the bridge set, and puts the moon back",
        })]
    private static object Set(JObject p)
    {
        var owner = DevBridgeMod.Instance;
        if (p.Bool("reset", false))
            Reset(owner);
        if (p["fog"] != null)
            RenderSettings.fogDensity = p.Float("fog");
        if (p["moon"] != null && Sky.Moon != null)
            Sky.Moon.intensity = p.Float("moon");
        if (p["sun"] != null && Sky.Sun != null)
        {
            Sky.Sun.intensity = p.Float("sun");
            Sky.Sun.enabled = Sky.Sun.intensity > 0f;
        }
        if (p["fogAmount"] != null)
            Sky.FogAmount.Set(owner, p.Float("fogAmount"));
        if (p["rainAmount"] != null)
            Sky.RainAmount.Set(owner, p.Float("rainAmount"));
        if (p["stormVolume"] != null)
            Sky.StormVolume.Set(owner, p.Float("stormVolume"));
        if (p["weather"] != null)
            SetWeather(owner, p);
        if (p["thunderMin"] != null)
            SetThunderDelay(owner, p);
        if (p["strikeIn"] != null && !Sky.StrikeIn(p.Float("strikeIn")))
            throw new System.InvalidOperationException("The thunder loop isn't waiting for a strike.");
        if (p.Bool("reschedule", false) && !Sky.Reschedule())
            throw new System.InvalidOperationException("The thunder loop isn't waiting for a strike.");
        return Get(p);
    }

    [BridgeCommand("rain.set", "Scale the rain's emission (all four systems), or lay one system's rate down directly.",
        Params = new[] { "amount?: multiplier, 1 is the scene's own, 0 is dry", "reset?: true clears the bridge's multiplier", "system?: a rain system's name, with rate?: its emission per second (no multiplier)" })]
    private static object RainSet(JObject p)
    {
        var owner = DevBridgeMod.Instance;
        if (p.Bool("reset", false))
            Sky.RainAmount.Clear(owner);
        if (p["amount"] != null)
            Sky.RainAmount.Set(owner, p.Float("amount"));
        if (p["system"] != null)
        {
            string name = p.Str("system");
            foreach (var system in Sky.RainSystems)
            {
                if (system != null && system.name == name)
                {
                    var emission = system.emission;
                    emission.rateOverTime = p.Float("rate");
                    return Rain();
                }
            }
            throw new System.ArgumentException($"No rain system called '{name}'.");
        }
        return Rain();
    }

    private static void Reset(DevBridgeMod owner)
    {
        if (Sky.Moon != null)
            Sky.Moon.intensity = Sky.MoonBaseIntensity;
        Sky.FogAmount.Clear(owner);
        Sky.RainAmount.Clear(owner);
        Sky.StormVolume.Clear(owner);
        Sky.SetThunderDelay(owner, null);
        Sky.SetWeather(owner, WeatherState.Default);
    }

    private static void SetWeather(DevBridgeMod owner, JObject p)
    {
        if (!WeatherState.TryParse(p.Str("weather"), out var kind))
            throw new System.ArgumentException("weather is drizzle, rain or storm.");
        WeatherKind? next = null;
        if (p["next"] != null)
        {
            if (!WeatherState.TryParse(p.Str("next"), out var forecast))
                throw new System.ArgumentException("next is drizzle, rain or storm.");
            next = forecast;
        }
        float nextAt = NeetModKit.Rules.ModFlags.PlaySeconds + p.Float("nextIn", 60f);
        Sky.SetWeather(owner, WeatherState.Now(kind, p.Float("intensity", 1f), next, nextAt));
    }

    private static void SetThunderDelay(DevBridgeMod owner, JObject p)
    {
        if (p.Str("thunderMin") == "default")
        {
            Sky.SetThunderDelay(owner, null);
            return;
        }
        float min = p.Float("thunderMin");
        float max = p.Float("thunderMax", min);
        Sky.SetThunderDelay(owner, (lo, hi) => Random.Range(min, max));
    }

    private static JObject Light(Light light, float scene) => light == null ? null : new JObject
    {
        ["name"] = light.name,
        ["enabled"] = light.enabled && light.gameObject.activeInHierarchy,
        ["intensity"] = light.intensity,
        ["sceneIntensity"] = scene >= 0f ? (JToken)scene : null,
        ["color"] = Color(light.color),
        ["shadows"] = light.shadows.ToString(),
        ["rotation"] = new JArray(light.transform.eulerAngles.x, light.transform.eulerAngles.y, light.transform.eulerAngles.z),
    };

    private static JObject Thunder()
    {
        var house = HouseManager._;
        return new JObject
        {
            ["loopAlive"] = Sky.ThunderLoopAlive,
            ["nextStrikeIn"] = Sky.NextStrikeIn,
            ["noThunder"] = house != null && house.noThunder,
            ["strikes"] = _strikes,
            ["lastStrikeAgo"] = _lastStrike >= 0f ? Time.time - _lastStrike : -1f,
        };
    }

    private static JArray Rain()
    {
        var list = new JArray();
        foreach (var system in Sky.RainSystems)
        {
            if (system == null)
                continue;
            var rate = system.emission.rateOverTime;
            list.Add(new JObject
            {
                ["name"] = system.name,
                ["playing"] = system.isPlaying,
                ["emitting"] = system.emission.enabled,
                ["rateMode"] = rate.mode.ToString(),
                ["rate"] = rate.mode == ParticleSystemCurveMode.Constant ? rate.constant : rate.curveMultiplier,
                ["maxParticles"] = system.main.maxParticles,
                ["particles"] = system.particleCount,
                ["amount"] = Sky.RainAmount.Value,
            });
        }
        return list;
    }

    private static JObject StormVolume()
    {
        var house = HouseManager._;
        return new JObject
        {
            ["volume"] = house != null && house.thunderStormAudio != null ? house.thunderStormAudio.volume : -1f,
            ["amount"] = Sky.StormVolume.Value,
        };
    }

    private static JObject Weather()
    {
        var weather = Sky.Weather;
        return new JObject
        {
            ["kind"] = weather.Kind.ToString(),
            ["name"] = weather.Name,
            ["intensity"] = weather.Intensity,
            ["startedAt"] = weather.StartedAt,
            ["age"] = weather.Age,
            ["next"] = weather.HasForecast ? weather.NextKind.Value.ToString() : null,
            ["secondsUntilNext"] = weather.SecondsUntilNext,
            ["owner"] = Sky.WeatherOwner,
        };
    }

    private static JArray Color(Color c) => new JArray(System.Math.Round(c.r, 3), System.Math.Round(c.g, 3), System.Math.Round(c.b, 3));
}
