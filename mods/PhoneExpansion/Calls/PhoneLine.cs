using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PhoneExpansion.Calls;

/// <summary>
/// The house's phones (one Phone_Dispatch with a Phone_Interface per handset). Each handset has four
/// audio sources: 0 voice, 1 button beep, 2 ringing, 3 busy tone.
/// </summary>
internal static class PhoneLine
{
    private const int RingingAudio = 2;
    private const int BusyAudio = 3;

    private static readonly AccessTools.FieldRef<Phone_Interface, string[]> CallableNumbers =
        AccessTools.FieldRefAccess<Phone_Interface, string[]>("callableNumbers");

    private static readonly AccessTools.FieldRef<Phone_Dispatch, Coroutine[]> RingCoroutines =
        AccessTools.FieldRefAccess<Phone_Dispatch, Coroutine[]>("prankCoroutines");

    private static readonly AccessTools.FieldRef<Phone_Dispatch, bool> LineBusy =
        AccessTools.FieldRefAccess<Phone_Dispatch, bool>("lineIsBusy");

    private static readonly Phone_Interface[] NoPhones = new Phone_Interface[0];
    private static Phone_Dispatch _dispatch;

    public static Phone_Dispatch Dispatch
    {
        get
        {
            if (_dispatch == null)
                _dispatch = Object.FindObjectOfType<Phone_Dispatch>();
            return _dispatch;
        }
    }

    public static Phone_Interface[] Phones => Dispatch != null ? Dispatch.phones : NoPhones;

    /// <summary>True when nothing is going on: no call, no ringing, nobody dialing, no conversation open.</summary>
    public static bool IsIdle
    {
        get
        {
            var dispatch = Dispatch;
            if (dispatch == null || LineBusy(dispatch) || (Dialogue._ != null && Dialogue._.currSpeaker != null))
                return false;
            foreach (var phone in dispatch.phones)
            {
                if (phone.talking || phone.composing || phone.ringing || phone.calling)
                    return false;
            }
            return true;
        }
    }

    /// <summary>Rings every handset like the game's incoming calls do, for <paramref name="seconds"/>.</summary>
    public static void RingAll(float seconds)
    {
        var dispatch = Dispatch;
        var routines = RingCoroutines(dispatch);
        for (int i = 0; i < dispatch.phones.Length; i++)
        {
            dispatch.phones[i].prankId = 1;
            routines[i] = dispatch.StartCoroutine(dispatch.phones[i].PrankCall(seconds));
        }
    }

    public static void PlayBusyTone(Phone_Interface phone) => phone.GetComponents<AudioSource>()[BusyAudio].Play();

    public static bool IsRinging(Phone_Interface phone) => phone.GetComponents<AudioSource>()[RingingAudio].isPlaying;

    /// <summary>Ends the call on <paramref name="phone"/> the way the game does when a call's last line is done.</summary>
    public static void HangUp(Phone_Interface phone) => phone.ClearScreen(noDistanceCheck: true, endPhoneCall: true);

    /// <summary>The handset the player is closest to.</summary>
    public static Phone_Interface Nearest(Vector3 position)
    {
        Phone_Interface best = null;
        float bestDistance = float.MaxValue;
        foreach (var phone in Phones)
        {
            float distance = Vector3.Distance(phone.transform.position, position);
            if (distance < bestDistance)
            {
                best = phone;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>Makes <paramref name="phone"/> answer the game's numbers plus <paramref name="extra"/>.</summary>
    public static void SetNumbers(Phone_Interface phone, IEnumerable<string> extra)
    {
        var numbers = new List<string>(GameNumbers.All);
        foreach (string digits in extra)
        {
            if (!numbers.Contains(digits))
                numbers.Add(digits);
        }
        CallableNumbers(phone) = numbers.ToArray();
    }

    public static void SetNumbersEverywhere(IEnumerable<string> extra)
    {
        var list = new List<string>(extra);
        foreach (var phone in Phones)
            SetNumbers(phone, list);
    }
}
