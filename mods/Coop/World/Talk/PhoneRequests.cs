using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Talk;

/// <summary>
/// The phones run on the host. A client pressing a handset's button is a request: the host presses
/// it on its handset with the remote player's body standing in as the game's "player near the phone"
/// (digits only count within 2.2 m), and remembers who pressed it last. The call that connects is
/// then that player's conversation (<see cref="HostTalks.BeforeInit"/>). Answering a ringing phone
/// is done for them directly, since the game only lets a player in the handset's zone answer.
///
/// WorldPhoneButton (client to host): handset index, button (0-9, -1 call or answer, -2 clear, -3 walked away).
/// </summary>
internal static class PhoneRequests
{
    private const int MaxPhones = 8;
    private const int WalkedAway = -3;

    // Host: who last pressed each handset's buttons.
    private static readonly byte[] Callers = new byte[MaxPhones];
    private static bool _remotePress;

    public static void Init() => Messages.On(MessageIds.WorldPhoneButton, OnButton);

    public static void Clear()
    {
        for (int i = 0; i < Callers.Length; i++)
            Callers[i] = WorldSide.None;
    }

    public static void Forget(byte slot)
    {
        for (int i = 0; i < Callers.Length; i++)
        {
            if (Callers[i] == slot)
                Callers[i] = WorldSide.None;
        }
    }

    /// <summary>Host: the player who's dialing or talking on <paramref name="speaker"/>'s handset, or none.</summary>
    public static byte CallerOf(CharacterStories speaker)
    {
        if (speaker == null || !speaker.TryGetComponent(out Phone_Interface phone))
            return WorldSide.None;
        int index = Speakers.PhoneIndex(phone);
        return index >= 0 && index < MaxPhones ? Callers[index] : WorldSide.None;
    }

    /// <summary>Client, before <c>Phone_Interface.ComposeNumber</c>: false keeps the press from acting here.</summary>
    public static bool BeforeCompose(Phone_Interface phone, int number)
    {
        if (!WorldSide.Running || !WorldSide.IsClient)
            return true;
        int index = Speakers.PhoneIndex(phone);
        if (index < 0)
            return false;
        // The button's beep at once; the screen follows from the host.
        phone.GetComponents<AudioSource>()[1].Play();
        Send(phone, number);
        return false;
    }

    private static void Send(Phone_Interface phone, int number)
    {
        int index = Speakers.PhoneIndex(phone);
        if (index < 0)
            return;
        var w = Outbox.Begin(MessageIds.WorldPhoneButton);
        w.Put((byte)index);
        w.Put((sbyte)number);
        Outbox.ToHost(w, Channel.Events);
    }

    /// <summary>
    /// Before <c>Phone_Interface.OnTriggerExit</c> (a player walked out of the handset's zone, which
    /// clears it): on a client the host clears it for this player; on the host, a remote player's
    /// call stays up when the host's player walks past.
    /// </summary>
    public static bool BeforeWalkAway(Phone_Interface phone, Collider other)
    {
        if (!WorldSide.Running)
            return true;
        if (WorldSide.IsHost)
            return !HostTalks.RemoteOnPhone(phone);
        if (other.gameObject == WorldSide.LocalPlayer)
            Send(phone, WalkedAway);
        return false;
    }

    /// <summary>Host, after its own player pressed a button: that handset is the host's again.</summary>
    public static void AfterCompose(Phone_Interface phone)
    {
        if (!WorldSide.Running || !WorldSide.IsHost)
            return;
        if (!_remotePress)
        {
            int index = Speakers.PhoneIndex(phone);
            if (index >= 0 && index < MaxPhones)
                Callers[index] = WorldSide.HostSlot;
        }
        PhoneScreens.SendSoon();
    }

    private static void OnButton(in Sender from, NetDataReader reader)
    {
        int index = reader.GetByte();
        int number = reader.GetSByte();
        if (!WorldSide.IsHost || !WorldSide.Running || index >= MaxPhones)
            return;
        var phone = Speakers.PhoneAt(index);
        var body = Speakers.BodyOf(from.Slot);
        if (phone == null || body == null)
            return;
        if (number == WalkedAway)
        {
            if (Callers[index] == from.Slot)
            {
                phone.ClearScreen(noDistanceCheck: true);
                Callers[index] = WorldSide.None;
            }
            return;
        }
        // Someone else's call on this handset stays theirs.
        byte talker = HostTalks.TalkerOf(phone.GetComponent<CharacterStories>());
        if (talker != WorldSide.None && talker != from.Slot)
            return;
        Callers[index] = from.Slot;
        if (number == -1 && phone.ringing)
        {
            Answer(phone, from.Slot);
            return;
        }
        var near = phone.player;
        phone.player = body;
        _remotePress = true;
        try
        {
            phone.ComposeNumber(number);
        }
        finally
        {
            _remotePress = false;
            phone.player = near;
        }
    }

    // Phone_Interface.PrankCall's answer, for a player outside the game's answer zone.
    private static void Answer(Phone_Interface phone, byte slot)
    {
        var sources = phone.GetComponents<AudioSource>();
        sources[1].Play();
        phone.phone_Dispatch.LineIsBusy = true;
        var speaker = phone.GetComponent<CharacterStories>();
        HostTalks.RunAs(slot, "", "", () => Dialogue._.InitCharacterDialogue(speaker, "Phone", "Phone.pPrank" + phone.prankId));
        sources[2].Stop();
        phone.phone_Dispatch.AnsweredPhone = phone;
        phone.ringing = false;
        phone.talking = true;
        PhoneScreens.SendSoon();
    }
}
