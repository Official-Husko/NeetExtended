using Coop.World.State.Types;

namespace Coop.World.State;

/// <summary>
/// Every shared state type, in a fixed order: the index is the type's id on the wire, so both games
/// must run the same build (the handshake checks the mod version). Add new types at the end.
/// </summary>
internal static class StateTypes
{
    public static readonly StateType[] All =
    {
        new DoorState(),
        new LightSwitchState(),
        new FuseButtonState(),
        new FuseSlotState(),
        new TvState(),
        new SinkState(),
        new GasState(),
        new FlashlightState(),
        new LighterState(),
    };

    static StateTypes()
    {
        for (int i = 0; i < All.Length; i++)
            All[i].Index = (byte)i;
    }

    public static StateType At(byte index) => index < All.Length ? All[index] : null;
}
