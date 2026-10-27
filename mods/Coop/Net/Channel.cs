namespace Coop.Net;

/// <summary>
/// The lanes messages travel on. Each keeps its own order, so a big join snapshot never holds up a
/// pose and a lost pose never holds up anything.
/// </summary>
public enum Channel : byte
{
    /// <summary>Newest wins and losses are fine: poses and transforms.</summary>
    Stream = 0,

    /// <summary>Reliable, in order: session messages, spawns, destroys, requests.</summary>
    Events = 1,

    /// <summary>Reliable, in order: changed state of things in the house.</summary>
    State = 2,

    /// <summary>Reliable, in order: the join snapshot and other big one-off payloads.</summary>
    Bulk = 3,

    /// <summary>
    /// Unreliable and unordered: the world's body and NPC streams, which span several packets per tick
    /// and drop stale entries themselves by the sender's time.
    /// </summary>
    WorldStream = 4,
}
