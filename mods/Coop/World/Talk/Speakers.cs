using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Talk;

/// <summary>Someone to talk to, the same on every machine: a shared NPC (or item) by its ref, or a phone handset by its place in the house's phone list.</summary>
internal struct SpeakerRef
{
    public const byte None = 0;
    public const byte Shared = 1;
    public const byte Phone = 2;

    public byte Kind;
    public NetRef Ref;
    public byte Index;

    public bool IsValid => Kind != None;

    public void Write(NetDataWriter w)
    {
        w.Put(Kind);
        if (Kind == Shared)
            Ref.Write(w);
        else if (Kind == Phone)
            w.Put(Index);
    }

    public static SpeakerRef Read(NetDataReader r)
    {
        var s = new SpeakerRef { Kind = r.GetByte() };
        if (s.Kind == Shared)
            s.Ref = NetRef.Read(r);
        else if (s.Kind == Phone)
            s.Index = r.GetByte();
        return s;
    }
}

/// <summary>Finds speakers (a <c>CharacterStories</c>) and the house's phones.</summary>
internal static class Speakers
{
    private static Phone_Dispatch _dispatch;

    public static void Clear() => _dispatch = null;

    public static Phone_Dispatch Dispatch()
    {
        if (_dispatch == null)
            _dispatch = Object.FindObjectOfType<Phone_Dispatch>(true);
        return _dispatch;
    }

    public static SpeakerRef Of(CharacterStories speaker)
    {
        if (speaker == null)
            return default;
        if (speaker.TryGetComponent(out Phone_Interface phone))
        {
            int index = PhoneIndex(phone);
            return index >= 0 ? new SpeakerRef { Kind = SpeakerRef.Phone, Index = (byte)index } : default;
        }
        for (var t = speaker.transform; t != null; t = t.parent)
        {
            var o = NetObjects.Find(t.gameObject);
            if (o != null)
                return new SpeakerRef { Kind = SpeakerRef.Shared, Ref = o.Ref };
        }
        return default;
    }

    public static CharacterStories Resolve(SpeakerRef s)
    {
        switch (s.Kind)
        {
            case SpeakerRef.Shared:
                var o = NetObjects.Find(s.Ref);
                return o != null && o.Alive ? o.Go.GetComponentInChildren<CharacterStories>(true) : null;
            case SpeakerRef.Phone:
                var phone = PhoneAt(s.Index);
                return phone != null ? phone.GetComponent<CharacterStories>() : null;
            default:
                return null;
        }
    }

    public static int PhoneIndex(Phone_Interface phone)
    {
        var phones = Dispatch()?.phones;
        if (phones == null || phone == null)
            return -1;
        for (int i = 0; i < phones.Length; i++)
        {
            if (phones[i] == phone)
                return i;
        }
        return -1;
    }

    public static Phone_Interface PhoneAt(int index)
    {
        var phones = Dispatch()?.phones;
        return phones != null && index >= 0 && index < phones.Length ? phones[index] : null;
    }

    /// <summary>A remote player's body in this game, or null.</summary>
    public static GameObject BodyOf(byte slot)
    {
        var puppet = Players.RemotePlayers.Find(slot);
        return puppet != null ? puppet.gameObject : null;
    }

    public static string NameOf(byte slot)
    {
        foreach (var player in Session.CoopSession.Players)
        {
            if (player.Slot == slot)
                return player.Name;
        }
        return "somebody";
    }
}
