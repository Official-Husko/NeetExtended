namespace Coop.World.Objects;

/// <summary>The per-body flags that travel with every pose.</summary>
internal static class BodyFlags
{
    /// <summary>The game wants the body kinematic (worn, boxed, socketed, stuck).</summary>
    public const byte Kinematic = 1;

    public const byte Active = 2;

    /// <summary>A scale follows the pose.</summary>
    public const byte Scale = 4;

    public const byte Held = 8;

    /// <summary>The flags that describe state (scale is only a payload marker).</summary>
    public const byte StateMask = Kinematic | Active | Held;

    public static byte Of(NetObject o)
    {
        byte flags = 0;
        if (o.Body != null && (o.IsReplica ? o.GameKinematic : o.Body.isKinematic))
            flags |= Kinematic;
        if (o.Go != null && o.Go.activeSelf)
            flags |= Active;
        if (o.Holder != WorldSide.None)
            flags |= Held;
        return flags;
    }
}
