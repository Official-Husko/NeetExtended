using System;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State;

/// <summary>
/// One kind of thing whose state is shared (a door's lock, a light switch, the TV...). It writes the
/// few bytes that matter and applies them with the game's own methods, never with its Load (most of
/// those restart the component). Change detection compares the written bytes, so a type only has to
/// say what to send and how to apply it.
/// </summary>
internal abstract class StateType
{
    /// <summary>Its place in <see cref="StateTypes.All"/>; also its id on the wire.</summary>
    public byte Index { get; internal set; }

    public abstract Type ComponentType { get; }

    /// <summary>The component lives on items, so it's only tracked while its item is shared.</summary>
    public virtual bool OnItems => false;

    public abstract void Write(Component component, NetDataWriter writer);

    public abstract void Apply(Component component, NetDataReader reader);
}

internal abstract class StateType<T> : StateType where T : Component
{
    public sealed override Type ComponentType => typeof(T);

    public sealed override void Write(Component component, NetDataWriter writer) => Write((T)component, writer);

    public sealed override void Apply(Component component, NetDataReader reader) => Apply((T)component, reader);

    protected abstract void Write(T component, NetDataWriter writer);

    protected abstract void Apply(T component, NetDataReader reader);
}
