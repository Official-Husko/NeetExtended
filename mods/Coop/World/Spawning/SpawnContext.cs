using System;

namespace Coop.World.Spawning;

/// <summary>
/// Moments when things appear or go away that must not be announced: the game loading a save (it
/// destroys and recreates everything), World applying what another game sent, and local changes World
/// makes itself (a forced drop after losing an item to someone else).
/// </summary>
internal static class SpawnContext
{
    private static int _loading;
    private static int _applying;
    private static int _quiet;

    public static bool Loading => _loading > 0;

    /// <summary>Making what another game sent (its spawn and its parts' saves).</summary>
    public static bool IsApplying => _applying > 0;

    /// <summary>Nothing that happens now is news for the other games.</summary>
    public static bool Silent => _loading > 0 || _applying > 0 || _quiet > 0;

    public static void BeginLoading() => _loading++;

    public static void EndLoading() => _loading = Math.Max(0, _loading - 1);

    public static Scope Applying() => new Scope(1);

    public static Scope Quiet() => new Scope(2);

    public static void Reset()
    {
        _loading = 0;
        _applying = 0;
        _quiet = 0;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly int _kind;

        public Scope(int kind)
        {
            _kind = kind;
            if (kind == 1)
                _applying++;
            else
                _quiet++;
        }

        public void Dispose()
        {
            if (_kind == 1)
                _applying = Math.Max(0, _applying - 1);
            else
                _quiet = Math.Max(0, _quiet - 1);
        }
    }
}
