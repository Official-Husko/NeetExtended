using System;
using NeetModKit.Game;

namespace Coop.Session;

/// <summary>
/// While this game is in a co-op session's house, tells the framework that others share it, so
/// nothing stops time for everyone (the mod menu and the cheat menu pause single player only).
/// </summary>
internal static class SharedHouse
{
    private static CoopMod _mod;
    private static IDisposable _hold;

    public static void Init(CoopMod mod)
    {
        _mod = mod;
        _hold = null;
        CoopLoop.Update += Tick;
    }

    private static void Tick()
    {
        bool shared = CoopSession.InWorld;
        if (shared && _hold == null)
            _hold = WorldRole.ShareWorld(_mod);
        else if (!shared && _hold != null)
        {
            _hold.Dispose();
            _hold = null;
        }
    }
}
