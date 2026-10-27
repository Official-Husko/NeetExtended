using NeetModKit.Game;
using UnityEngine;

namespace Coop.World.Talk.Watch;

/// <summary>
/// Whether a player can take in someone else's conversation: the speaker is in the same room, or
/// there is a clear line between them. The line is the one the game uses before a speaker starts
/// talking (<c>CharacterStories.HasSpeakerLineOfSight</c>): walls and closed doors block it, so an
/// open doorway lets the conversation through and the room next door doesn't hear it.
/// </summary>
internal static class Earshot
{
    private const int MaxHits = 16;
    private const float SpeakerHeight = 1.4f;
    private const float ListenerHeight = 1.5f;

    private static readonly RaycastHit[] Hits = new RaycastHit[MaxHits];
    private static int _mask;
    private static int _wall = -1;

    public static bool Hears(Vector3 speaker, Vector3 listener) => Clear(speaker, listener) || SameRoom(speaker, listener);

    public static bool SameRoom(Vector3 a, Vector3 b) => Rooms.At(a) == Rooms.At(b);

    public static bool Clear(Vector3 speaker, Vector3 listener)
    {
        if (_wall < 0)
        {
            _wall = LayerMask.NameToLayer("Construction");
            _mask = LayerMask.GetMask("Usable", "Construction");
        }
        var from = speaker + Vector3.up * SpeakerHeight;
        var offset = listener + Vector3.up * ListenerHeight - from;
        float distance = offset.magnitude;
        int count = Physics.RaycastNonAlloc(from, offset / distance, Hits, distance, _mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = Hits[i].collider;
            if (hit.gameObject.layer == _wall || hit.TryGetComponent(out Door _))
                return false;
        }
        return true;
    }
}
