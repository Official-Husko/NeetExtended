using Coop.World.Objects;
using Coop.World.Ownership;
using UnityEngine;

namespace Coop.World.Talk;

/// <summary>
/// Offerings (the candy bucket) in a remote player's conversation. The host's bucket comes out at the
/// speaker as in its own conversations, and the talker's game shows its own bucket at its copy of the
/// speaker. The item the talker throws or drops moves on the host too and lands in the host's bucket,
/// where the game's ProcessOffering would take it for the host's conversation; World hands it to the
/// talker's instead: the host takes the item over, runs the knot's "got something" branch with it,
/// and hides it like the game does (the story may give it back or destroy it later).
/// </summary>
internal static class Offerings
{
    /// <summary>Host, from an OfferingTime tag in <paramref name="c"/>'s step (AdvanceStory's bucket).</summary>
    public static void BucketOn(Conversation c)
    {
        c.OfferingTime = true;
        var bucket = c.Speaker.bucket;
        if (bucket == null || bucket.bucketActivated)
            return;
        Dialogue._.StartCoroutine(bucket.ActivateBucket(c.Speaker.gameObject));
        bucket.bucketActivated = true;
    }

    /// <summary>Host: the offering arrived (ProcessOffering's bucket).</summary>
    public static void BucketOff(Conversation c)
    {
        c.BucketOff = true;
        var bucket = c.Speaker.bucket;
        if (bucket == null || !bucket.bucketActivated || bucket.targetOwner != c.Speaker.gameObject)
            return;
        Dialogue._.StartCoroutine(bucket.DeactivateBucket());
        bucket.bucketActivated = false;
    }

    /// <summary>Host, before Dialogue.ProcessOffering: false when the bucket was held out for a remote player's conversation.</summary>
    public static bool BeforeProcess(GameObject item)
    {
        if (!WorldSide.Running || !WorldSide.IsHost || item == null || TalkRunner.Current != null || Dialogue._ == null)
            return true;
        var bucket = Dialogue._.bucket;
        var owner = bucket != null ? bucket.targetOwner : null;
        var speaker = owner != null ? owner.GetComponent<CharacterStories>() : null;
        var c = speaker != null ? HostTalks.Of(speaker) : null;
        if (c == null || item.GetComponent<Offering>() == null)
            return true;
        var o = NetObjects.Find(item);
        if (o != null)
            Owners.HostTake(o);
        TalkRunner.Offered(c, item);
        item.SetActive(false);
        item.transform.position += Vector3.up * 500f + Random.insideUnitSphere * 75f;
        TalkSender.Line(c);
        return false;
    }

    /// <summary>Client: the bucket at this game's copy of the speaker, out or away.</summary>
    public static void Show(CharacterStories speaker, bool on)
    {
        var d = Dialogue._;
        var bucket = d != null ? d.bucket : null;
        if (bucket == null || speaker == null)
            return;
        if (on && !bucket.bucketActivated)
        {
            d.StartCoroutine(bucket.ActivateBucket(speaker.gameObject));
            bucket.bucketActivated = true;
        }
        else if (!on && bucket.bucketActivated)
        {
            d.StartCoroutine(bucket.DeactivateBucket());
            bucket.bucketActivated = false;
        }
    }
}
