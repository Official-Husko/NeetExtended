using Coop.Net;
using LiteNetLib.Utils;
using NeetModKit.Game;

namespace Coop.World.Economy;

/// <summary>
/// One wallet for the house, kept on the host. Every write to the computer's money on a client
/// (orders, apps, stocks, tax bills, story money) becomes a delta for the host, which applies it the
/// game's way (taxes and all) and sends everyone the new total. Clients show their change straight
/// away and take the host's total when it comes. Hooked through <see cref="Patches.MoneyPatches"/>.
/// </summary>
internal static class MoneySync
{
    private const float CheckInterval = 1f;

    // Host: the total last sent. Client: the total as we last knew it (host's word or our own write).
    private static float _lastTotal = float.NaN;
    private static float _known = float.NaN;
    private static float _nextCheck;

    public static void Init()
    {
        Messages.On(MessageIds.MoneyDelta, OnDelta);
        Messages.On(MessageIds.MoneyTotal, OnTotal);
    }

    public static void Reset()
    {
        _lastTotal = float.NaN;
        _known = float.NaN;
    }

    /// <summary>
    /// Once a second: money changed without the setter (mods and cheats write the field directly)
    /// goes out the same way as a setter write.
    /// </summary>
    public static void Check()
    {
        float now = UnityEngine.Time.time;
        var computer = HouseManager._?.mainComputer;
        if (now < _nextCheck || computer == null)
            return;
        _nextCheck = now + CheckInterval;
        float money = computer.Money;
        if (WorldSide.IsHost)
        {
            if (money != _lastTotal)
                AfterSet(computer);
            return;
        }
        if (float.IsNaN(_known))
        {
            _known = money;
            return;
        }
        if (money != _known)
        {
            SendDelta(money - _known);
            _known = money;
        }
    }

    /// <summary>Client, from the money setter: false keeps the game from setting it itself.</summary>
    public static bool BeforeSet(Computer computer, float value)
    {
        if (!WorldSide.Running || !WorldSide.IsClient || computer != HouseManager._?.mainComputer)
            return true;
        float delta = value - computer.Money;
        if (delta != 0f)
        {
            Money.SetUntaxed(value);
            _known = value;
            SendDelta(delta);
        }
        return false;
    }

    private static void SendDelta(float delta)
    {
        var w = Outbox.Begin(MessageIds.MoneyDelta);
        w.Put(delta);
        Outbox.ToHost(w, Channel.Events);
    }

    /// <summary>Host, after the money setter ran.</summary>
    public static void AfterSet(Computer computer)
    {
        if (!WorldSide.Running || !WorldSide.IsHost || computer != HouseManager._?.mainComputer)
            return;
        float total = computer.Money;
        if (total == _lastTotal)
            return;
        _lastTotal = total;
        Outbox.ToPlayers(Total(total), Channel.Events);
    }

    /// <summary>Host: the current total to one player (joining, resyncing).</summary>
    public static void SendTo(byte slot)
    {
        var computer = HouseManager._?.mainComputer;
        if (computer != null)
            Outbox.ToSlot(slot, Total(computer.Money), Channel.Events);
    }

    private static NetDataWriter Total(float total)
    {
        var w = Outbox.Begin(MessageIds.MoneyTotal);
        w.Put(total);
        return w;
    }

    private static void OnDelta(in Sender from, NetDataReader reader)
    {
        float delta = reader.GetFloat();
        var computer = HouseManager._?.mainComputer;
        if (!WorldSide.IsHost || !WorldSide.Running || computer == null)
            return;
        // Through the setter: increases are taxed like the host's own, and AfterSet sends the total.
        computer.Money += delta;
    }

    private static void OnTotal(in Sender from, NetDataReader reader)
    {
        float total = reader.GetFloat();
        if (WorldSide.IsHost || !WorldSide.Running || HouseManager._?.mainComputer == null)
            return;
        Money.SetUntaxed(total);
        _known = total;
    }

    /// <summary>Client: the total as the host's manifest has it.</summary>
    public static void ApplyTotal(float total)
    {
        if (HouseManager._?.mainComputer == null)
            return;
        Money.SetUntaxed(total);
        _known = total;
    }
}
