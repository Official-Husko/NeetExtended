using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>
/// How big a dust ball is: its level, which sets its size and what it is worth in a vacuum or to the
/// Queen of Dust. The game rolls it at random when the ball starts, so every game would roll its
/// own; the one that made the ball sends it. A ball that swallows another changes it where the
/// merge happens (see ItemStatePatches).
///
/// Layout: level (byte).
/// </summary>
internal sealed class DustBallState : StateType<DustBall>
{
    public override bool OnItems => true;

    protected override void Write(DustBall ball, NetDataWriter w) => w.Put((byte)Mathf.Clamp(ball.Level, 0, 255));

    protected override void Apply(DustBall ball, NetDataReader r)
    {
        int level = r.GetByte();
        if (level > 0 && ball.Level != level)
            ball.Level = level;
    }

    /// <summary>
    /// The ball's own Start rolls a level, and it runs a frame after the ball was made (after the
    /// level has arrived): put the level the others sent back.
    /// </summary>
    public static void Restore(DustBall ball)
    {
        var t = StateTrackers.Find(ball);
        if (t == null || t.Object == null || !t.Object.IsReplica || t.Last.Length == 0 || t.Last[0] == 0)
            return;
        ball.Level = t.Last[0];
    }
}
