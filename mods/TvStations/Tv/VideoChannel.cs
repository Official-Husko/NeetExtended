using NeetLoader.Logging;
using TvStations.Stations;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TvStations.Tv;

/// <summary>
/// A station on the TV: a copy of one of the game's channel objects (so it sits on the screen the
/// same way) with a RawImage showing a VideoPlayer's picture and an AudioSource set up like the
/// TV's own speaker. It has the game's ScreenModule component, so the TV keeps it in its channel
/// list like any other channel.
/// </summary>
internal sealed class VideoChannel
{
    private const string ReportSource = "Custom TV Stations";

    private readonly GameObject _object;
    private readonly RawImage _picture;
    private readonly VideoPlayer _player;
    private readonly AudioSource _audio;
    private readonly AudioSource _tvSpeaker;
    private RenderTexture _texture;

    public VideoChannel(TV tv, ScreenModule template, Station station)
    {
        Station = station;
        _tvSpeaker = tv.audioTv;

        _object = Object.Instantiate(template.gameObject, template.transform.parent);
        _object.name = "Channel_Mod_" + station.Name;
        _object.SetActive(false);
        for (int i = _object.transform.childCount - 1; i >= 0; i--)
            Object.Destroy(_object.transform.GetChild(i).gameObject);

        // A UI object takes one Graphic: the channel's Image makes way for the video's RawImage.
        var image = _object.GetComponent<Image>();
        if (image != null)
            Object.DestroyImmediate(image);
        _picture = _object.AddComponent<RawImage>();
        _picture.color = Color.black;

        Module = _object.GetComponent<ScreenModule>();
        Module.tv = tv;
        Module.randomClips = null;

        _audio = _object.AddComponent<AudioSource>();
        CopySpeaker(_tvSpeaker, _audio);

        _player = _object.AddComponent<VideoPlayer>();
        _player.playOnAwake = false;
        _player.source = VideoSource.Url;
        _player.url = station.File;
        _player.isLooping = true;
        _player.skipOnDrop = true;
        _player.renderMode = VideoRenderMode.RenderTexture;
        _player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        _player.controlledAudioTrackCount = 1;
        _player.EnableAudioTrack(0, true);
        _player.SetTargetAudioSource(0, _audio);
        _player.prepareCompleted += OnPrepared;
        _player.errorReceived += OnError;
    }

    public Station Station { get; }

    /// <summary>The component the TV's channel list holds.</summary>
    public ScreenModule Module { get; }

    /// <summary>Why the video doesn't play, or null.</summary>
    public string Problem { get; private set; }

    public bool IsPlaying => _player != null && _player.isPlaying;

    public void Show(float volume)
    {
        _audio.volume = _tvSpeaker.volume * Station.Volume * volume;
        _object.SetActive(true);
        // Not there yet (co-op is still fetching the host's video): a black screen until the list
        // is loaded again with it, rather than an error.
        if (!System.IO.File.Exists(Station.File))
            return;
        _player.Play();
    }

    public void SetVolume(float volume) => _audio.volume = _tvSpeaker.volume * Station.Volume * volume;

    public void Destroy()
    {
        if (_player != null)
            _player.Stop();
        if (_object != null)
            Object.Destroy(_object);
        if (_texture != null)
        {
            _texture.Release();
            Object.Destroy(_texture);
        }
    }

    private void OnPrepared(VideoPlayer player)
    {
        // Match the video's size so it isn't scaled twice.
        int width = Mathf.Max(16, (int)player.width), height = Mathf.Max(16, (int)player.height);
        if (_texture == null || _texture.width != width || _texture.height != height)
        {
            if (_texture != null)
            {
                _texture.Release();
                Object.Destroy(_texture);
            }
            _texture = new RenderTexture(width, height, 0) { name = "TvStation_" + Station.Name };
        }
        player.targetTexture = _texture;
        _picture.texture = _texture;
        _picture.color = Color.white;
        Problem = null;
    }

    private void OnError(VideoPlayer player, string message)
    {
        Problem = message;
        _picture.color = Color.black;
        TvStationsMod.Instance?.Log.Warn($"{Station.Source} doesn't play: {message}");
        ErrorReport.Add(ReportSource, $"Couldn't play {Station.Source}",
            message + "\nIf it's an MP4, converting it to WebM (VP8) usually helps, for example:\n" +
            "ffmpeg -i input.mp4 -c:v libvpx -b:v 2M -c:a libvorbis output.webm");
    }

    private static void CopySpeaker(AudioSource from, AudioSource to)
    {
        to.playOnAwake = false;
        to.loop = false;
        to.outputAudioMixerGroup = from.outputAudioMixerGroup;
        to.spatialBlend = from.spatialBlend;
        to.rolloffMode = from.rolloffMode;
        to.minDistance = from.minDistance;
        to.maxDistance = from.maxDistance;
        to.dopplerLevel = from.dopplerLevel;
        to.spread = from.spread;
        to.priority = from.priority;
        if (from.rolloffMode == AudioRolloffMode.Custom)
            to.SetCustomCurve(AudioSourceCurveType.CustomRolloff, from.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
    }
}
