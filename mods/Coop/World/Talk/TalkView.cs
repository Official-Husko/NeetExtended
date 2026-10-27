using System.Collections.Generic;
using Coop.World.Talk.Watch;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Coop.World.Talk;

/// <summary>
/// This game's dialogue box, driven from outside the story: a line with the game's typing effect and
/// the speaker's voice blips (<c>Dialogue.AdvanceStory</c> and <c>PlayVoiceAudio</c>), the choice
/// buttons (<c>UpdateChoices</c>), and short lines that aren't part of a conversation ("busy").
/// The same box shows another player's conversation to whoever is near (<see cref="Watching"/>),
/// for show only: nothing in it can be clicked.
/// </summary>
internal static class TalkView
{
    private const float ToastTime = 2.5f;

    private static Coroutine _typing;

    // Voice blips, played from Tick instead of a coroutine.
    private static AudioSource _voice;
    private static bool _oneShot;
    private static int _blipsLeft;
    private static float _nextBlip;

    /// <summary>For tests: how many voice blips played, and from what, in this game.</summary>
    public static int Blips { get; private set; }

    public static string LastVoice { get; private set; } = "";

    private static string _toast;
    private static float _toastUntil;

    public static void Clear()
    {
        _typing = null;
        _voice = null;
        _blipsLeft = 0;
        _toast = null;
    }

    public static void ShowLine(CharacterStories speaker, string text, bool isChoice)
    {
        var d = Dialogue._;
        if (d == null || d.storyText == null)
            return;
        _toast = null;
        d.storyText.enabled = true;
        d.GetComponent<CanvasGroup>().interactable = true;
        HideChoices();
        Show(d, speaker, text, isChoice);
    }

    /// <summary>A line that isn't a conversation, gone after a moment unless a conversation takes the box.</summary>
    public static void Toast(CharacterStories speaker, string text)
    {
        var d = Dialogue._;
        if (d == null || d.storyText == null || d.currSpeaker != null)
            return;
        d.storyText.enabled = true;
        Show(d, speaker, text, false);
        _toast = text;
        _toastUntil = Time.time + ToastTime;
    }

    /// <summary>Another player's line: typed out like the game's, in a box nobody can click. With a talker, the voice plays too (theirs at their body).</summary>
    public static void ShowWatched(CharacterStories speaker, string text, bool playerVoice, Transform talker, bool voice)
    {
        var d = Dialogue._;
        if (d == null || d.storyText == null)
            return;
        _toast = null;
        d.storyText.enabled = true;
        d.GetComponent<CanvasGroup>().interactable = false;
        HideChoices();
        d.storyText.SetText(text);
        Type(d);
        if (voice)
            Voice(d, speaker, text, playerVoice, talker);
        else
            _blipsLeft = 0;
    }

    /// <summary>Takes another player's line away.</summary>
    public static void HideWatched()
    {
        var d = Dialogue._;
        if (d == null || d.storyText == null)
            return;
        StopVoice();
        if (_typing != null)
        {
            d.StopCoroutine(_typing);
            _typing = null;
        }
        d.storyText.SetText("");
        d.storyText.enabled = false;
        HideChoices();
    }

    public static void StopVoice() => _blipsLeft = 0;

    /// <summary>Only the choice another player picked stays up.</summary>
    public static void ShowPicked(List<string> choices, int index)
    {
        var d = Dialogue._;
        if (d == null || d.optionButtons == null)
            return;
        for (int i = 0; i < d.optionButtons.Count; i++)
        {
            var button = d.optionButtons[i];
            bool shown = i == index;
            button.gameObject.SetActive(shown);
            var label = shown ? button.GetComponentInChildren<Text>() : null;
            if (label != null)
                label.text = choices[index];
        }
    }

    /// <summary>The choice buttons; with <paramref name="interactive"/> off (another player's) they only show.</summary>
    public static void ShowChoices(List<string> choices, bool interactive = true)
    {
        var d = Dialogue._;
        if (d == null || d.optionButtons == null)
            return;
        d.GetComponent<GraphicRaycaster>().enabled = interactive;
        for (int i = 0; i < d.optionButtons.Count; i++)
        {
            var button = d.optionButtons[i];
            bool shown = i < choices.Count;
            button.gameObject.SetActive(shown);
            if (!shown)
                continue;
            if (i == 0 && interactive && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.text = choices[i];
        }
    }

    public static void HideChoices()
    {
        var d = Dialogue._;
        if (d == null || d.optionButtons == null)
            return;
        foreach (var button in d.optionButtons)
            button.gameObject.SetActive(false);
        d.GetComponent<GraphicRaycaster>().enabled = false;
    }

    /// <summary>Every frame: voice blips, and a short line timing out.</summary>
    public static void Tick()
    {
        float now = Time.time;
        if (_blipsLeft > 0 && now >= _nextBlip)
        {
            if (_voice != null)
            {
                if (!_oneShot)
                    _voice.pitch = Random.Range(0.8f, 1.4f);
                _voice.Play();
                LastVoice = _voice.name + " at " + _voice.transform.position.ToString("0.0");
                Blips++;
            }
            _blipsLeft--;
            _nextBlip = now + Random.Range(0.1f, 0.2f);
        }
        if (_toast != null && now > _toastUntil)
        {
            var d = Dialogue._;
            if (d != null && d.currSpeaker == null && d.storyText != null && d.storyText.text == _toast)
            {
                d.storyText.SetText("");
                d.storyText.enabled = false;
            }
            _toast = null;
            Watching.Invalidate();
        }
    }

    private static void Show(Dialogue d, CharacterStories speaker, string text, bool isChoice)
    {
        d.storyText.SetText(text);
        Type(d);
        Voice(d, speaker, text, isChoice, null);
    }

    private static void Type(Dialogue d)
    {
        if (_typing != null)
            d.StopCoroutine(_typing);
        var typing = d.storyText.GetComponent<TextAnimations>();
        _typing = typing != null ? d.StartCoroutine(typing.AnimateVertexColors(Dialogue.TextAnimationType.Simple)) : null;
    }

    // Dialogue.PlayVoiceAudio's recipe: a blip every 0.1-0.2 s, about one per two words.
    // With a talker (another player's line), the player's voice comes from their body.
    private static void Voice(Dialogue d, CharacterStories speaker, string text, bool isChoice, Transform talker)
    {
        _oneShot = speaker != null && speaker.soundOneShot;
        if (_oneShot)
        {
            var sources = speaker.GetComponents<AudioSource>();
            _voice = sources.Length > 0 ? sources[Random.Range(0, sources.Length)] : null;
            _blipsLeft = 1;
        }
        else
        {
            _voice = isChoice || speaker == null ? PlayerVoice(d, talker) : SpeakerVoice(d, speaker, talker);
            _blipsLeft = 1 + Random.Range(0, 2) + Mathf.Max(0, Mathf.RoundToInt(Words(text) * 0.42f));
        }
        _nextBlip = Time.time;
    }

    private static AudioSource SpeakerVoice(Dialogue d, CharacterStories speaker, Transform talker)
    {
        if (speaker.TryGetComponent(out AudioSource own) && own.clip != null)
        {
            if (speaker.dialogueAudio != null)
                own.clip = speaker.dialogueAudio;
            return own;
        }
        return PlayerVoice(d, talker);
    }

    private static AudioSource PlayerVoice(Dialogue d, Transform talker)
    {
        if (talker != null)
        {
            var world = WatchVoice.At(d, talker.position + Vector3.up * 1.5f);
            if (world != null)
                return world;
        }
        return d.TryGetComponent(out AudioSource voice) ? voice : null;
    }

    private static int Words(string text)
    {
        int words = 1;
        foreach (char c in text)
        {
            if (c == ' ')
                words++;
        }
        return words;
    }
}
