using Coop.Net;
using HarmonyLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Economy;

/// <summary>
/// The anime card game on the computer earns crystals over time, which only the host runs. Twice a
/// second the host sends the crystals and every card that changed. A client still clicks, buys and
/// promotes on its own copy for instant feedback, and the same action runs on the host as a request,
/// whose state then wins. Redeeming crystals for money is a request only, so the money comes once.
///
/// WorldCards (host to all): crystals, unlocked count, card count, per card: index, level, medal,
/// locked, exp, exp to level, income, price, base income, base price.
/// WorldCardAction (client to host): action, card index.
/// </summary>
internal static class CardSync
{
    public const byte Click = 0;
    public const byte Buy = 1;
    public const byte Promote = 2;
    public const byte Redeem = 3;

    private const float Interval = 0.5f;
    private const int Fields = 9;

    private static readonly AccessTools.FieldRef<AnimeCardGenerator, AnimeCard[]> Cards =
        AccessTools.FieldRefAccess<AnimeCardGenerator, AnimeCard[]>("cards");

    // What was last sent, Fields numbers per card, and which cards go out this time.
    private static float[] _sent = new float[0];
    private static bool[] _pending = new bool[0];
    private static float _sentCrystals = float.NaN;
    private static float _next;

    public static void Init()
    {
        Messages.On(MessageIds.WorldCards, OnCards);
        Messages.On(MessageIds.WorldCardAction, OnAction);
    }

    public static void Clear()
    {
        _sent = new float[0];
        _pending = new bool[0];
        _sentCrystals = float.NaN;
    }

    public static void SendAgain() => Clear();

    /// <summary>Host, twice a second.</summary>
    public static void Tick()
    {
        float now = Time.time;
        var game = Game();
        var cards = game != null ? Cards(game) : null;
        if (now < _next || cards == null)
            return;
        _next = now + Interval;
        if (_pending.Length != cards.Length)
        {
            // Never sent (or the card count changed): every card goes out.
            _sent = new float[cards.Length * Fields];
            for (int i = 0; i < _sent.Length; i++)
                _sent[i] = float.NaN;
            _pending = new bool[cards.Length];
        }

        int changed = 0;
        for (int i = 0; i < cards.Length; i++)
        {
            if (Changed(cards[i], i))
                changed++;
        }
        if (changed == 0 && game.Crystals == _sentCrystals)
            return;
        _sentCrystals = game.Crystals;
        var w = Outbox.Begin(MessageIds.WorldCards);
        w.Put(game.Crystals);
        w.Put(game.maxCardUnlocked);
        w.Put((ushort)changed);
        for (int i = 0; i < cards.Length; i++)
        {
            if (!_pending[i])
                continue;
            _pending[i] = false;
            Write(w, cards[i], i);
        }
        Outbox.ToPlayers(w, Channel.Events);
    }

    /// <summary>Client, from the card game's buttons. False keeps the action from running here.</summary>
    public static bool Request(byte action, AnimeCard card)
    {
        if (!WorldSide.Running || !WorldSide.IsClient)
            return true;
        int index = card != null ? IndexOf(card) : 0;
        if (card != null && index < 0)
            return true;
        var w = Outbox.Begin(MessageIds.WorldCardAction);
        w.Put(action);
        w.Put((ushort)index);
        Outbox.ToHost(w, Channel.Events);
        // Clicking, buying and promoting show at once here; redeeming waits for the host's money.
        return action != Redeem;
    }

    private static bool Changed(AnimeCard card, int index)
    {
        int at = index * Fields;
        bool changed = Set(at, card.Level) | Set(at + 1, card.medalLvl) | Set(at + 2, card.Locked ? 1 : 0)
            | Set(at + 3, Mathf.Round(card.exp * 10f)) | Set(at + 4, card.expToLevel) | Set(at + 5, card.Income)
            | Set(at + 6, card.purchasePrice) | Set(at + 7, card.initIncome) | Set(at + 8, card.initPrice);
        _pending[index] = changed;
        return changed;
    }

    private static bool Set(int at, float value)
    {
        if (_sent[at] == value)
            return false;
        _sent[at] = value;
        return true;
    }

    private static void Write(NetDataWriter w, AnimeCard card, int index)
    {
        w.Put((ushort)index);
        w.Put((short)card.Level);
        w.Put((byte)card.medalLvl);
        w.Put(card.Locked);
        w.Put(card.exp);
        w.Put(card.expToLevel);
        w.Put(card.Income);
        w.Put(card.purchasePrice);
        w.Put(card.initIncome);
        w.Put(card.initPrice);
    }

    private static void OnCards(in Sender from, NetDataReader reader)
    {
        var game = Game();
        var cards = game != null ? Cards(game) : null;
        if (WorldSide.IsHost || !WorldSide.Running || cards == null)
            return;
        game.Crystals = reader.GetFloat();
        game.maxCardUnlocked = reader.GetInt();
        int count = reader.GetUShort();
        for (int i = 0; i < count; i++)
        {
            int index = reader.GetUShort();
            var card = index < cards.Length ? cards[index] : null;
            CardState.Apply(card, reader);
        }
    }

    private static void OnAction(in Sender from, NetDataReader reader)
    {
        byte action = reader.GetByte();
        int index = reader.GetUShort();
        var game = Game();
        var cards = game != null ? Cards(game) : null;
        if (!WorldSide.IsHost || !WorldSide.Running || cards == null)
            return;
        var card = index < cards.Length ? cards[index] : null;
        switch (action)
        {
            case Click when card != null: card.ClickedCard(); break;
            case Buy when card != null: card.BuyCard(); break;
            case Promote when card != null: card.PromoteCard(); break;
            case Redeem: game.RedeemCrystals(); break;
        }
    }

    private static int IndexOf(AnimeCard card)
    {
        var game = Game();
        var cards = game != null ? Cards(game) : null;
        return cards != null ? System.Array.IndexOf(cards, card) : -1;
    }

    private static AnimeCardGenerator Game() => HouseManager._?.mainComputer?.animeCardGenerator;
}
