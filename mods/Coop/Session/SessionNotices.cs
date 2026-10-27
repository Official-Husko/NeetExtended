using System.Collections.Generic;
using NeetModKit.Ui.Notifications;

namespace Coop.Session;

/// <summary>
/// Notices in the corner for who comes and goes: "Bob is joining" on the host while they get the
/// files and the house, "Bob joined" and "Bob left" for everyone, whose house a client just walked
/// into, and why a client's game ended. A hot reload doesn't announce everyone again.
/// Setting: Show players joining and leaving.
/// </summary>
internal static class SessionNotices
{
    private const float Linger = 4f;
    private const string OwnLeave = "You left the game.";

    private static readonly Dictionary<byte, Notice> Joining = new Dictionary<byte, Notice>();
    private static readonly HashSet<string> Announced = new HashSet<string>();
    private static readonly List<byte> Gone = new List<byte>();
    private static bool _client;

    public static void Init()
    {
        CoopSession.PlayerArrived += OnArrived;
        CoopSession.PlayerReady += OnReady;
        CoopSession.PlayerLeft += OnLeft;
        CoopSession.WorldReady += OnWorldReady;
        CoopSession.Started += () => _client = CoopSession.IsClient;
        CoopSession.Resumed += () =>
        {
            _client = CoopSession.IsClient;
            RememberEveryone();
        };
        CoopSession.Ended += OnEnded;
        CoopLoop.Update += Tick;
    }

    private static bool On => CoopMod.Config.Value.ShowPlayerNotices && CoopMod.Instance != null;

    private static void OnArrived(CoopPlayer player)
    {
        if (!On)
            return;
        Close(player.Slot);
        Joining[player.Slot] = Notices.Show(CoopMod.Instance, player.Name + " is joining", "getting the files and the house");
    }

    private static void OnReady(CoopPlayer player)
    {
        if (player.IsLocal || !Announced.Add(Key(player)))
            return;
        if (Joining.TryGetValue(player.Slot, out var joining) && joining.IsOpen)
        {
            Joining.Remove(player.Slot);
            joining.Title = player.Name + " joined";
            joining.Detail = InTheHouse();
            joining.Close(Linger);
            return;
        }
        Close(player.Slot);
        if (On)
            Notices.Show(CoopMod.Instance, player.Name + " joined", InTheHouse(), seconds: Linger);
    }

    private static void OnLeft(CoopPlayer player)
    {
        Announced.Remove(Key(player));
        Close(player.Slot);
        if (On && !CoopSession.IsEnding)
            Notices.Show(CoopMod.Instance, player.Name + " left", InTheHouse(), seconds: Linger);
    }

    // Client: in someone's house now; the ones already there aren't news.
    private static void OnWorldReady()
    {
        RememberEveryone();
        if (!CoopSession.IsClient || !On)
            return;
        string host = CoopSession.FindSlot(CoopPlayer.HostSlot)?.Name ?? "the host";
        Notices.Show(CoopMod.Instance, $"In {host}'s house", InTheHouse(), seconds: Linger);
    }

    private static void OnEnded(string reason)
    {
        bool wasClient = _client;
        _client = false;
        foreach (var notice in Joining.Values)
            notice.Close();
        Joining.Clear();
        Announced.Clear();
        if (On && wasClient && reason != OwnLeave)
            Notices.Show(CoopMod.Instance, "Out of the co-op game", reason, seconds: Linger * 2f);
    }

    private static void RememberEveryone()
    {
        foreach (var player in CoopSession.Players)
        {
            if (player.InWorld || player.IsLocal)
                Announced.Add(Key(player));
        }
    }

    // A player who goes before they're in never gets a "left" (they never joined): say so here.
    private static void Tick()
    {
        if (Joining.Count == 0)
            return;
        Gone.Clear();
        foreach (var pair in Joining)
        {
            if (CoopSession.FindSlot(pair.Key) == null || !pair.Value.IsOpen)
                Gone.Add(pair.Key);
        }
        foreach (byte slot in Gone)
        {
            var notice = Joining[slot];
            Joining.Remove(slot);
            if (!notice.IsOpen)
                continue;
            notice.Detail = "left before getting in";
            notice.Close(Linger);
        }
    }

    private static void Close(byte slot)
    {
        if (Joining.TryGetValue(slot, out var notice))
        {
            notice.Close();
            Joining.Remove(slot);
        }
    }

    private static string InTheHouse()
    {
        int count = 0;
        foreach (var player in CoopSession.Players)
        {
            if (player.InWorld || player.IsLocal)
                count++;
        }
        return count == 1 ? "nobody else here" : $"{count} players in the house";
    }

    private static string Key(CoopPlayer player) => player.Slot + "/" + player.Id;
}
