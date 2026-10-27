using System.Collections.Generic;
using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;
using NeetModKit.Saves;

namespace Coop.Players;

/// <summary>
/// Host: every player's body as they last sent it (stats, status, clothes, where they stood), kept in
/// the host's save by player id, so someone who comes back gets their own body back instead of a new one.
/// </summary>
internal static class BodyBook
{
    internal sealed class Book
    {
        /// <summary>Player id to the body's save entry (the game's own Brain save, as JSON).</summary>
        public Dictionary<string, string> Bodies = new Dictionary<string, string>();
    }

    private static Book _book = new Book();

    public static void Init(CoopMod mod)
    {
        ModSaveData.Register(mod, () => _book, book => _book = book ?? new Book());
        Messages.On(MessageIds.Body, OnBody);
    }

    /// <summary>The body last saved for <paramref name="playerId"/>, or null.</summary>
    public static string Find(string playerId) =>
        !string.IsNullOrEmpty(playerId) && _book.Bodies.TryGetValue(playerId, out string body) ? body : null;

    private static void OnBody(in Sender from, NetDataReader reader)
    {
        var player = CoopSession.FindSlot(from.Slot);
        if (!CoopSession.IsHost || player == null || string.IsNullOrEmpty(player.Id))
            return;
        _book.Bodies[player.Id] = reader.GetString();
    }
}
