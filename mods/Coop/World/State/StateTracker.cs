using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.State;

/// <summary>One shared component: what it is, what was last sent or applied, and whether to look again soon.</summary>
internal sealed class StateTracker
{
    public NetRef Ref;
    public StateType Type;
    public Component Component;

    /// <summary>The item or door it sits on, if any (that decides who looks for changes).</summary>
    public NetObject Object;

    public byte[] Last = System.Array.Empty<byte>();
    public bool Dirty;
    internal int ComponentId;

    public (long, byte) Key => (Ref.Key, Type.Index);

    /// <summary>The game that should notice changes the game makes on its own.</summary>
    public bool WatchedHere => Object != null ? !Object.IsReplica : WorldSide.IsHost;

    public bool Same(byte[] data, int length)
    {
        if (Last.Length != length)
            return false;
        for (int i = 0; i < length; i++)
        {
            if (Last[i] != data[i])
                return false;
        }
        return true;
    }

    public void Remember(byte[] data, int offset, int length)
    {
        if (Last.Length != length)
            Last = new byte[length];
        System.Buffer.BlockCopy(data, offset, Last, 0, length);
    }
}
