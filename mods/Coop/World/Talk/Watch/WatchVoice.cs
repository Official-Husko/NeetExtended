using UnityEngine;

namespace Coop.World.Talk.Watch;

/// <summary>
/// The talker's voice blips at the talker's body, for the players watching. The dialogue box's own
/// voice is a flat sound in the talker's ears; this is a copy of it that sits in the world.
/// </summary>
internal static class WatchVoice
{
    private const string ObjectName = "CoopWatchVoice";

    private static AudioSource _source;

    public static AudioSource At(Dialogue d, Vector3 position)
    {
        if (_source == null)
        {
            var original = d.GetComponent<AudioSource>();
            if (original == null)
                return null;
            // One per game: a copy left by an earlier copy of the mod (hot reload) is reused.
            var go = GameObject.Find(ObjectName) ?? new GameObject(ObjectName);
            _source = go.GetComponent<AudioSource>() ?? go.AddComponent<AudioSource>();
            _source.clip = original.clip;
            _source.outputAudioMixerGroup = original.outputAudioMixerGroup;
            _source.volume = original.volume;
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 2f;
            _source.maxDistance = 14f;
        }
        _source.transform.position = position;
        return _source;
    }

    public static void Clear()
    {
        if (_source != null)
            Object.Destroy(_source.gameObject);
        _source = null;
    }
}
