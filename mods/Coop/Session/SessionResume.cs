using Coop.Net;
using NeetModKit.Events;
using Newtonsoft.Json.Linq;

namespace Coop.Session;

/// <summary>
/// Picks up a session the previous copy of the mod parked in a hot reload, once its transport is
/// there again: direct is built in, others come from mods that load after this one (Steam).
/// <see cref="Begin"/> restores the session before the mod's parts start, so they see it;
/// <see cref="Finish"/> announces it (Resumed, resync) once they all listen.
/// </summary>
internal static class SessionResume
{
    private static CoopMod _mod;
    private static JObject _parked;
    private static bool _started;
    private static bool _announce;

    public static void Begin(CoopMod mod)
    {
        _mod = mod;
        _started = false;
        _announce = false;
        _parked = SessionMemory.TakeParked();
        if (_parked != null && !TryResume())
            Transports.Registered += OnRegistered;
    }

    /// <summary>Call after every part of the mod has started.</summary>
    public static void Finish()
    {
        _started = true;
        if (_announce)
            Announce();
    }

    private static void OnRegistered(TransportOption option)
    {
        if (_parked != null)
            TryResume();
    }

    private static bool TryResume()
    {
        var option = Transports.Find(SessionMemory.TransportOf(_parked));
        if (option == null)
            return false;
        var parked = _parked;
        _parked = null;
        Transports.Registered -= OnRegistered;

        var transport = option.Create();
        if (!transport.IsRunning)
        {
            transport.Stop();
            return true;
        }
        CoopSession.Use(option, transport);
        SessionMemory.Restore(parked);
        CoopMod.Log.Info($"Carried the co-op session over the reload ({option.Name}, {CoopSession.State}, {CoopSession.Players.Count} players).");
        if (_started)
            Announce();
        else
            _announce = true;
        return true;
    }

    // Once: now if the house is up, else on the next house load (WhenHouseReady would repeat it on every load).
    private static void Announce()
    {
        _announce = false;
        if (GameEvents.IsHouseReady)
        {
            Raise();
            return;
        }
        System.IDisposable waiting = null;
        waiting = _mod.Track(GameEvents.HouseReady.Subscribe(() =>
        {
            if (waiting == null)
                return;
            waiting.Dispose();
            waiting = null;
            Raise();
        }));
    }

    private static void Raise()
    {
        CoopSession.RaiseResumed();
        Handshake.Resync();
    }
}
