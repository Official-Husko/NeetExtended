using System;
using Coop.Net;
using LiteNetLib.Utils;
using NeetModKit.Game;
using NeetModKit.Rules;
using UnityEngine;

namespace Coop.World.Ambience;

/// <summary>
/// The weather. The host's mods drive it (<see cref="Sky.Weather"/>: drizzle, rain or storm with a
/// strength and a forecast); the host sends it whenever it changes and to a player who joins or asks, and
/// a client sets it in its own game with Co-op as the owner. Each game's mods then apply the weather to
/// their own scene (rain, wind, fog, getting wet), so nothing but this small state travels. Times go as
/// seconds (how long the weather has been going, how long until the forecast) because every game has its
/// own play clock.
///
/// WorldWeather (host to clients): kind, intensity, age, forecast kind (255 = none), seconds until it.
/// WorldWeatherAsk (client to host): nothing; the host answers with WorldWeather to that player.
/// </summary>
internal static class WeatherSync
{
    private const byte NoForecast = byte.MaxValue;
    private const float SameStart = 1.5f;

    public static void Init()
    {
        Messages.On(MessageIds.WorldWeather, OnWeather);
        Messages.On(MessageIds.WorldWeatherAsk, OnAsk);
        Sky.WeatherChanged.Subscribe(CoopMod.Instance, OnChanged);
    }

    /// <summary>Client, once its world is up (also after a Co-op reload): the host's weather, please.</summary>
    public static void Ask()
    {
        if (!WorldSide.IsClient)
            return;
        Outbox.ToHost(Outbox.Begin(MessageIds.WorldWeatherAsk), Channel.Events);
    }

    /// <summary>Host: tells one player what the weather is, if anything is driving it.</summary>
    public static void SendTo(byte slot)
    {
        if (!WorldSide.IsHost || Sky.Weather.Equals(WeatherState.Default))
            return;
        Outbox.ToSlot(slot, Write(Sky.Weather), Channel.Events);
    }

    private static void OnChanged(WeatherState state)
    {
        if (WorldSide.Running && WorldSide.IsHost)
            Outbox.ToPlayers(Write(state), Channel.Events);
    }

    private static NetDataWriter Write(WeatherState state)
    {
        var w = Outbox.Begin(MessageIds.WorldWeather);
        w.Put((byte)state.Kind);
        w.Put(state.Intensity);
        w.Put(Mathf.Max(0f, state.Age));
        w.Put(state.HasForecast ? (byte)state.NextKind.Value : NoForecast);
        w.Put(state.HasForecast ? state.SecondsUntilNext : 0f);
        return w;
    }

    private static void OnAsk(in Sender from, NetDataReader reader)
    {
        if (WorldSide.IsHost)
            SendTo(from.Slot);
    }

    private static void OnWeather(in Sender from, NetDataReader reader)
    {
        if (WorldSide.IsHost)
            return;
        byte kind = reader.GetByte();
        float intensity = reader.GetFloat();
        float age = reader.GetFloat();
        byte next = reader.GetByte();
        float until = reader.GetFloat();
        if (!Enum.IsDefined(typeof(WeatherKind), (int)kind) || float.IsNaN(intensity) || float.IsNaN(age) || float.IsNaN(until))
            return;

        float now = ModFlags.PlaySeconds;
        bool forecast = next != NoForecast && Enum.IsDefined(typeof(WeatherKind), (int)next);
        var state = new WeatherState((WeatherKind)kind, intensity, now - Mathf.Max(0f, age),
            forecast ? (WeatherKind?)(WeatherKind)next : null, now + Mathf.Max(0f, until));
        if (!IsNews(state))
            return;
        Sky.SetWeather(CoopMod.Instance, state);
    }

    // The same weather arriving twice (an answer to a question and the host's own send) isn't set twice.
    private static bool IsNews(WeatherState state)
    {
        var current = Sky.Weather;
        return current.Kind != state.Kind || current.Intensity != state.Intensity || current.NextKind != state.NextKind ||
               Math.Abs(current.StartedAt - state.StartedAt) > SameStart;
    }
}
