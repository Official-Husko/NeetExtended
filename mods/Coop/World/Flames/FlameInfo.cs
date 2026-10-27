using Coop.World.Objects;

namespace Coop.World.Flames;

/// <summary>One shared flame: where it is in the house, and what was last sent or applied for it.</summary>
internal sealed class FlameInfo
{
    public Flame Flame;
    public int Id;

    /// <summary>The item it sits on, or (for a flame in the scene) a hash of where it sits.</summary>
    public NetRef Ref;

    /// <summary>Its place among the flames of that item (0 for a scene flame).</summary>
    public byte Index;

    /// <summary>A flame of the scene itself, not of an item.</summary>
    public bool Scene;

    /// <summary>Packed state (<see cref="FlameBook.Pack"/>) as the other games last heard it.</summary>
    public byte Last;

    public bool Pending;

    /// <summary>Added to an item when it caught fire; gone when the fire is.</summary>
    public bool Runtime;

    public long Key => Ref.Key * 256 + Index;
}
