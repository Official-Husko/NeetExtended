using System.Collections;
using DevBridge.Server;
using NeetModKit.Game;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Watches one lightning strike frame by frame for <c>sky.strike measure</c>.</summary>
internal static class SkyMeasure
{
    // A lamp that dips with the lightning: the first powered, switched-on light switch whose parent isn't a numbered copy.
    private static Light FlickeringLamp()
    {
        foreach (var lampSwitch in HouseManager._.lightSwitches)
        {
            string parent = lampSwitch.transform.parent != null ? lampSwitch.transform.parent.name : "";
            if (!lampSwitch.switchedOn || !lampSwitch.IsPowered || parent.EndsWith(")"))
                continue;
            foreach (var light in lampSwitch.linkedLights)
            {
                if (light != null && light.enabled)
                    return light;
            }
        }
        return null;
    }

    // Samples the moon every frame from the strike until it's back down, in real time (the game's own
    // flash steps once per frame, so frames and seconds both say something).
    public static IEnumerator Strike(Deferred result)
    {
        var moon = Sky.Moon;
        if (moon == null || !Sky.Strike())
        {
            result.Fail("No house to strike.");
            yield break;
        }
        var lamp = FlickeringLamp();
        float lampStart = lamp != null ? lamp.intensity : 0f;
        float lampDipped = 0f;
        float start = Time.unscaledTime;
        float peak = 0f;
        float above = 0f;
        float struckAt = -1f;
        int frames = 0;
        int framesAbove = 0;
        bool flashed = false;
        using (Sky.OnStrike.Subscribe(() => struckAt = Time.unscaledTime - start))
        {
            while (Time.unscaledTime - start < 15f)
            {
                yield return null;
                frames++;
                float level = moon.intensity;
                peak = Mathf.Max(peak, level);
                if (lamp != null && lamp.intensity < lampStart * 0.9f)
                    lampDipped += Time.unscaledDeltaTime;
                if (level > 1f)
                {
                    flashed = true;
                    above += Time.unscaledDeltaTime;
                    framesAbove++;
                }
                else if (flashed)
                {
                    break;
                }
            }
        }
        yield return new WaitForSecondsRealtime(0.5f); // The fade ends and the moon is set back.
        result.Complete(new JObject
        {
            ["secondsAbove1"] = above,
            ["framesAbove1"] = framesAbove,
            ["peak"] = peak,
            ["strikeEventAt"] = struckAt,
            ["lampSecondsDipped"] = lamp != null ? (JToken)lampDipped : null,
            ["secondsToEnd"] = Time.unscaledTime - start,
            ["frames"] = frames,
            ["avgFps"] = frames / Mathf.Max(0.001f, Time.unscaledTime - start),
            ["moonNow"] = moon.intensity,
        });
    }
}
