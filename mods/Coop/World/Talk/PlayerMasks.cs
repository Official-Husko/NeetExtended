using System.Collections.Generic;
using System.Text;
using Ink.Runtime;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Talk;

/// <summary>
/// What the story asks about the player it talks to: what they hold (<c>playerHeld</c>) and their
/// body (<c>playerBody</c>), which <c>Dialogue.CheckPlayerHeldItems</c> and <c>CheckPlayerBody</c>
/// read from the player before every line. A remote player's game reads its own player and sends
/// the item names with each step; the host puts them in the story before running it. Like the
/// game, an empty list leaves the story's value as it was.
/// </summary>
internal static class PlayerMasks
{
    private const string Held = "playerHeld";
    private const string Body = "playerBody";
    private const string OfferingList = "OfferingStates";
    private const string PlayerList = "PlayerStates";

    private static readonly List<string> Names = new List<string>();
    private static readonly StringBuilder Text = new StringBuilder();

    /// <summary>Client: this game's player, as two comma-separated lists.</summary>
    public static void Write(NetDataWriter w)
    {
        var player = WorldSide.LocalPlayer;
        var story = Dialogue._ != null ? Dialogue._.masterStory : null;
        string held = "", body = "";
        if (player != null && story != null && player.TryGetComponent(out Player_Abilities hands))
        {
            held = HeldOf(hands, story);
            body = BodyOf(player, hands);
        }
        w.Put(held);
        w.Put(body);
        Players.PlayerFacts.Write(w);
    }

    /// <summary>Host: reads what <see cref="Write"/> sent; the rules' facts are kept for <paramref name="slot"/>.</summary>
    public static void Read(NetDataReader r, byte slot, out string held, out string body)
    {
        held = r.GetString();
        body = r.GetString();
        Players.PlayerFacts.Read(r, slot);
    }

    /// <summary>Host, before a step: the talking player's lists go into the story.</summary>
    public static void Apply(Story story, string held, string body)
    {
        if (!string.IsNullOrEmpty(held))
            story.variablesState[Held] = List(story, OfferingList, held);
        if (!string.IsNullOrEmpty(body))
            story.variablesState[Body] = List(story, PlayerList, body);
    }

    private static InkList List(Story story, string origin, string names)
    {
        var list = new InkList(origin, story);
        foreach (string name in names.Split(','))
        {
            try
            {
                list.AddItem(name);
            }
            catch (System.Exception)
            {
                // A name this story's list doesn't have (another version of a mod): left out.
            }
        }
        return list;
    }

    // Dialogue.CheckPlayerHeldItems: both hands' offering states, merged.
    private static string HeldOf(Player_Abilities hands, Story story)
    {
        Names.Clear();
        for (int i = 0; i < 2 && i < hands.pickups.Length; i++)
        {
            var item = hands.pickups[i];
            var offering = item != null ? item.GetComponent<Offering>() : null;
            if (offering == null)
                continue;
            foreach (var entry in offering.OfferingStatsToInkList(story))
            {
                if (!Names.Contains(entry.Key.itemName))
                    Names.Add(entry.Key.itemName);
            }
        }
        return Join();
    }

    // Dialogue.CheckPlayerBody.
    private static string BodyOf(GameObject player, Player_Abilities hands)
    {
        Names.Clear();
        if (hands.neetRend != null)
        {
            if (hands.neetRend.GetBlendShapeWeight(2) > 33f)
                Names.Add("fat");
            if (hands.neetRend.GetBlendShapeWeight(4) > 50f)
                Names.Add("muscle");
        }
        bool woman = player.TryGetComponent(out PlayerStats stats) && stats.bodyType != null && stats.bodyType.GetVal() > 50f;
        if (woman)
            Names.Add("woman");
        var clothes = hands.clothings;
        if (clothes != null && clothes.Length > 2 && (clothes[1] == null || (woman && clothes[2] == null)))
            Names.Add("naked");
        return Join();
    }

    private static string Join()
    {
        Text.Clear();
        for (int i = 0; i < Names.Count; i++)
        {
            if (i > 0)
                Text.Append(',');
            Text.Append(Names[i]);
        }
        return Text.ToString();
    }
}
