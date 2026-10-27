using UnityEngine;

namespace Coop.World.Forwarding;

/// <summary>
/// Stands in for another player's stats on their body here. The house finds people to hit by their
/// stats (NPCs and cops look for them, punches, kicks, knives, guns and explosions hurt them); a hit
/// on this goes to the player's own game, where their real body takes it the game's way. Its health
/// follows what that player reports, so nobody keeps hitting someone who's down.
/// </summary>
internal sealed class RemoteActor : ActorStats
{
    private Health _health;

    public byte Slot { get; private set; }

    public void Init(byte slot)
    {
        Slot = slot;
        _health = new Health();
        RegisterStats(_health);
        // Stat.Affect passes (position, potency, starting value, kind, force, source).
        _health.manageEffect = (position, potency, start, kind, force, source) =>
        {
            Forwarder.HitPlayer(Slot, position, potency, kind, force);
            return 0f;
        };
    }

    public void ShowHealth(float value) => _health?.ForceSetVal(value);
}
