using UnityEngine;

namespace Coop.World.Talk;

/// <summary>Host: what a handset does to a remote player's call (<see cref="HostTalks"/> keeps the conversations).</summary>
internal static class HostPhoneTalks
{
    /// <summary>Host, before a handset's ClearScreen: a remote player's call ends with it, and only the host's own call closes the host's dialogue box.</summary>
    public static void BeforeClearScreen(Phone_Interface phone, bool noDistanceCheck, ref bool endPhoneCall)
    {
        if (!WorldSide.Running || !WorldSide.IsHost || TalkRunner.Current != null)
            return;
        Conversation c;
        var speaker = phone.GetComponent<CharacterStories>();
        if (endPhoneCall && Dialogue._ != null && Dialogue._.currSpeaker != speaker)
            endPhoneCall = false;
        bool acts = noDistanceCheck || (phone.player != null && Vector3.Distance(phone.player.transform.position, phone.transform.position) < 2.2f);
        if (acts && speaker != null && (c = HostTalks.Of(speaker)) != null)
            HostTalks.End(c, hangUp: false, tell: true);
    }

    /// <summary>Host: true when a remote player is on <paramref name="phone"/> or dialing it (the host walking past leaves it alone).</summary>
    public static bool RemoteOnPhone(Phone_Interface phone)
    {
        var speaker = phone.GetComponent<CharacterStories>();
        if (speaker != null && HostTalks.Of(speaker) != null)
            return true;
        byte caller = speaker != null ? PhoneRequests.CallerOf(speaker) : WorldSide.None;
        return caller != WorldSide.None && caller != WorldSide.HostSlot;
    }
}
