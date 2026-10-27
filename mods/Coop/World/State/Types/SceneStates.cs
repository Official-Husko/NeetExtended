using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>A door's lock (a key turned on one machine). Its swing travels as a body.</summary>
internal sealed class DoorState : StateType<Door>
{
    protected override void Write(Door door, NetDataWriter w) => w.Put(door.Locked);

    protected override void Apply(Door door, NetDataReader r)
    {
        bool locked = r.GetBool();
        if (door.Locked != locked)
            door.Locked = locked;
    }
}

/// <summary>
/// A wall switch. Use() flips it the way a hand does (lever, click, lamps); a three-way partner's flag
/// flips with it and the partner's own state follows from the host.
/// </summary>
internal sealed class LightSwitchState : StateType<LightSwitch>
{
    protected override void Write(LightSwitch light, NetDataWriter w) => w.Put(light.switchedOn);

    protected override void Apply(LightSwitch light, NetDataReader r)
    {
        if (light.switchedOn != r.GetBool())
            light.Use();
    }
}

/// <summary>
/// A sink or bath tap: running or not, and how high the water stands. Every game fills and drains
/// the water itself from the tap, but a glass dipped in only lowers it where the glass is simulated,
/// so the level is sent too (in 32nds, which is a change about every second while it fills).
/// Toggle turns the stream, sound and the tap itself.
///
/// Layout: pouring (bool), level (byte; the water goes from -0.5 to 1.1, sent from 0 to 51).
/// </summary>
internal sealed class SinkState : StateType<SinkFaucet>
{
    // A tap's water sits elsewhere in the scene than the tap (the bath's lever is not in the bath).
    private static readonly System.Collections.Generic.Dictionary<int, SinkFaucet> Taps = new System.Collections.Generic.Dictionary<int, SinkFaucet>();

    public static void Clear() => Taps.Clear();

    /// <summary>The tap that fills this water, once its state has been read (every tap is when sync starts).</summary>
    public static SinkFaucet TapOf(WaterVolume water) => Taps.TryGetValue(water.GetInstanceID(), out var tap) ? tap : null;

    protected override void Write(SinkFaucet sink, NetDataWriter w)
    {
        Taps[sink.waterVolume.GetInstanceID()] = sink;
        w.Put(sink.pouring);
        w.Put((byte)Mathf.Clamp(Mathf.RoundToInt((sink.waterVolume.Volume + 0.5f) * 32f), 0, 51));
    }

    protected override void Apply(SinkFaucet sink, NetDataReader r)
    {
        if (sink.pouring != r.GetBool())
            sink.Toggle();
        float level = r.GetByte() / 32f - 0.5f;
        if (Mathf.Abs(sink.waterVolume.Volume - level) > 1.5f / 32f)
            sink.waterVolume.Volume = level;
    }
}

/// <summary>A stove knob: gas on or off. What the gas then does (lighting, exploding) stays where it happens.</summary>
internal sealed class GasState : StateType<GasButton>
{
    protected override void Write(GasButton gas, NetDataWriter w) => w.Put(gas.gasLeaking);

    protected override void Apply(GasButton gas, NetDataReader r)
    {
        if (gas.gasLeaking != r.GetBool())
            gas.Toggle();
    }
}

/// <summary>The TV: on or off and which channel.</summary>
internal sealed class TvState : StateType<TV>
{
    private const int Off = -3;
    private const int On = -2;

    protected override void Write(TV tv, NetDataWriter w)
    {
        w.Put(tv.isOn);
        w.Put((short)tv.displayedScreenModule);
    }

    protected override void Apply(TV tv, NetDataReader r)
    {
        bool on = r.GetBool();
        int channel = r.GetShort();
        if (!on)
        {
            if (tv.isOn)
                tv.Control(Off);
            return;
        }
        if (!tv.isOn)
            tv.Control(On);
        if (channel >= 0 && tv.displayedScreenModule != channel)
            tv.Control(channel);
    }
}
