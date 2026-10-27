using LiteNetLib.Utils;

namespace Coop.World.State.Types;

/// <summary>
/// Whether something got soaked (in a bath, by water from the tap, by a wet floor) and with what: the
/// wet look and sticky stay on it. Soaking happens where the thing is simulated. Food that turns
/// into its soaked version when it gets wet is replaced, and that arrives as a spawn.
///
/// Layout: soaked (bool), liquid (byte, the paint channel). All zero is dry.
/// </summary>
internal sealed class SoakerState : StateType<Soaker>
{
    public override bool OnItems => true;

    protected override void Write(Soaker soaker, NetDataWriter w)
    {
        w.Put(soaker.soaked);
        w.Put((byte)(soaker.soaked ? soaker.paintChannel : 0));
    }

    protected override void Apply(Soaker soaker, NetDataReader r)
    {
        bool soaked = r.GetBool();
        int liquid = r.GetByte();
        if (!soaked || soaker.soaked)
            return;
        var food = soaker.GetComponent<Food>();
        if (food != null && food.swapOnWaterObj != null)
            return; // Replaced where it got wet.
        soaker.Soak(liquid);
    }
}
