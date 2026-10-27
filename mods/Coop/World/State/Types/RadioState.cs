using Coop.World.Objects;
using HarmonyLib;
using LiteNetLib.Utils;

namespace Coop.World.State.Types;

/// <summary>
/// A radio: which battery and cassette are in it, the lid and the music switch. The cassette and
/// battery are shared items moved by their own streams; this only tells the radio what it holds.
/// The tape starts from the top on each machine (the play position isn't shared).
/// </summary>
internal sealed class RadioState : StateType<Radio>
{
    private static readonly AccessTools.FieldRef<Radio, bool> On = AccessTools.FieldRefAccess<Radio, bool>("on");

    public override bool OnItems => true;

    protected override void Write(Radio radio, NetDataWriter w)
    {
        RefOf(radio.Fuse != null ? radio.Fuse.gameObject : null).Write(w);
        RefOf(radio.cassette != null ? radio.cassette.gameObject : null).Write(w);
        w.Put(radio.opened);
        w.Put(On(radio));
    }

    protected override void Apply(Radio radio, NetDataReader r)
    {
        var fuse = Find<Fuse>(NetRef.Read(r), out bool fuseKnown);
        var cassette = Find<Cassette>(NetRef.Read(r), out bool cassetteKnown);
        bool opened = r.GetBool();
        bool on = r.GetBool();

        if (fuseKnown && radio.Fuse != fuse)
            radio.Fuse = fuse;
        if (cassetteKnown && radio.cassette != cassette)
            Load(radio, cassette);
        if (radio.opened != opened)
            radio.Opened = opened;
        if (On(radio) != on)
            radio.MusicSwitch();
    }

    // The radio's own insert and eject move the tape and hand it out; here only the radio changes.
    private static void Load(Radio radio, Cassette cassette)
    {
        radio.cassette = cassette;
        radio.lastCassette = cassette;
        radio.musicAudio.Stop();
        radio.musicAudio.clip = cassette != null ? cassette.musicClip : null;
        if (cassette != null && radio.Powered && On(radio))
            radio.musicAudio.Play();
    }

    private static NetRef RefOf(UnityEngine.GameObject go)
    {
        var o = go != null ? NetObjects.Find(go) : null;
        return o != null ? o.Ref : default;
    }

    // Known: the ref is "nothing", or a shared thing that's here (not one that hasn't arrived yet).
    private static T Find<T>(NetRef r, out bool known) where T : UnityEngine.Component
    {
        if (!r.IsValid)
        {
            known = true;
            return null;
        }
        var o = NetObjects.Find(r);
        known = o != null;
        return o != null ? o.Go.GetComponent<T>() : null;
    }
}
