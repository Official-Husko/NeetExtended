using System.Text;
using Coop.World.Objects;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Coop.World.Talk;

/// <summary>
/// Conversations and phones from the dev bridge, through the same game calls a player's input makes:
/// <c>neet call type.call type=Coop.World.Talk.TalkTestApi method=Talk args=[3311] --instance 2</c>.
/// </summary>
public static class TalkTestApi
{
    private static readonly System.Action<Dialogue> UpdateChoices =
        AccessTools.MethodDelegate<System.Action<Dialogue>>(AccessTools.Method(typeof(Dialogue), "UpdateChoices"));

    /// <summary>Shared things that can talk: id, name, whether they're leaving or the player is in range, who talks to them (host).</summary>
    public static string Speakers()
    {
        var text = new StringBuilder();
        foreach (var o in NetObjects.All)
        {
            if (!o.Alive)
                continue;
            var speaker = o.Go.GetComponentInChildren<CharacterStories>(true);
            if (speaker == null)
                continue;
            var p = o.Tf.position;
            text.Append(o.Ref.Id).Append(' ').Append(speaker.characterName).Append(" at ").Append(p.ToString("0.0"))
                .Append(speaker.leaving ? " leaving" : "").Append(speaker.playerInRange ? " in range" : "");
            if (WorldSide.IsHost)
            {
                byte talker = HostTalks.TalkerOf(speaker);
                if (talker != WorldSide.None)
                    text.Append(" talking to ").Append(talker);
            }
            text.Append('\n');
        }
        return text.ToString();
    }

    /// <summary>Starts talking to a shared NPC the way CharacterStories does (how: 0 talk, 1 harm, 2 disgust).</summary>
    public static string Talk(int id, int how = 0)
    {
        var o = NetObjects.Find(new NetRef(NetKind.Npc, id)) ?? NetObjects.Find(new NetRef(NetKind.Item, id));
        var speaker = o != null && o.Alive ? o.Go.GetComponentInChildren<CharacterStories>(true) : null;
        if (speaker == null || Dialogue._ == null)
            return $"Nothing shared with id {id} can talk.";
        string name = speaker.characterName;
        if (how == TalkProtocol.Harm)
            Dialogue._.InitCharacterDialogue(speaker, name + "_Harm", name + "Harm", true, true);
        else if (how == TalkProtocol.Disgust)
            Dialogue._.InitCharacterDialogue(speaker, name + "_Disgust", name + "Disgust", true, true);
        else
        {
            string knot = speaker.useFullName ? name
                : name + (speaker.useNightId ? "Night" + EncounterSpawner.nightId : "") + (speaker.overrideKnot != "" ? "." + speaker.overrideKnot : "");
            Dialogue._.InitCharacterDialogue(speaker, name, knot, speaker.overrideKnotChanged, speaker.overrideKnotChanged);
        }
        return Box();
    }

    /// <summary>Use pressed in the conversation.</summary>
    public static string Use()
    {
        var d = Dialogue._;
        if (d == null)
            return "No dialogue.";
        if (WorldSide.IsClient)
            ClientTalk.UsePressed();
        else if (d.currSpeaker != null && d.storyText.enabled)
        {
            if (d.masterStory.canContinue)
                d.AdvanceStory(isChoice: false);
            else
                UpdateChoices(d);
        }
        return Box();
    }

    public static string Choose(int choice)
    {
        Dialogue._?.MakeChoice(choice);
        return Box();
    }

    public static string Leave()
    {
        Dialogue._?.CloseDialogue();
        return Box();
    }

    /// <summary>This game's dialogue box: who, the text, the choice buttons that show.</summary>
    public static string Box()
    {
        var d = Dialogue._;
        if (d == null || d.storyText == null)
            return "No dialogue.";
        var text = new StringBuilder();
        text.Append(d.currSpeaker != null ? d.currSpeaker.characterName : "nobody").Append(d.storyText.enabled ? ", shown: " : ", hidden: ")
            .Append(d.storyText.text);
        foreach (var button in d.optionButtons)
        {
            if (!button.gameObject.activeSelf)
                continue;
            var label = button.GetComponentInChildren<Text>();
            text.Append("\n  [").Append(label != null ? label.text : "?").Append(']');
        }
        return text.ToString();
    }

    /// <summary>Host: a shared item lands in the candy bucket (what the bucket's trigger does).</summary>
    public static string Offer(int itemId)
    {
        var o = NetObjects.Find(new NetRef(NetKind.Item, itemId));
        if (o == null || !o.Alive || Dialogue._ == null)
            return $"No shared item {itemId}.";
        var bucket = Dialogue._.bucket;
        Dialogue._.ProcessOffering(o.Go);
        return $"Offered {o.Go.name}; bucket for {(bucket != null && bucket.targetOwner != null ? bucket.targetOwner.name : "nobody")}.";
    }

    /// <summary>Presses a handset's button the way a hand does (0-9, -1 call or answer, -2 clear).</summary>
    public static string Press(int phone, int button)
    {
        var handset = global::Coop.World.Talk.Speakers.PhoneAt(phone);
        if (handset == null)
            return $"No phone {phone}.";
        handset.ComposeNumber(button);
        return Phones();
    }

    public static string Phones()
    {
        var phones = global::Coop.World.Talk.Speakers.Dispatch()?.phones;
        if (phones == null)
            return "No phones.";
        var text = new StringBuilder();
        for (int i = 0; i < phones.Length; i++)
        {
            var phone = phones[i];
            if (phone == null)
                continue;
            text.Append(i).Append(' ').Append(phone.name).Append(" at ").Append(phone.transform.position.ToString("0.0")).Append(": \"")
                .Append(phone.screenText != null ? phone.screenText.text : "").Append('"')
                .Append(phone.ringing ? " ringing" : "").Append(phone.talking ? " talking" : "").Append(phone.calling ? " calling" : "");
            if (WorldSide.IsHost)
                text.Append(" caller ").Append(PhoneRequests.CallerOf(phone.GetComponent<CharacterStories>()));
            text.Append('\n');
        }
        var dispatch = global::Coop.World.Talk.Speakers.Dispatch();
        if (dispatch != null)
            text.Append("line busy: ").Append(dispatch.LineIsBusy);
        return text.ToString();
    }
}
