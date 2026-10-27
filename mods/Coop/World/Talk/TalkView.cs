using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Coop.World.Talk;

/// <summary>
/// This game's dialogue box, driven from outside the story: a line with the game's typing effect and
/// the speaker's voice blips (<c>Dialogue.AdvanceStory</c> and <c>PlayVoiceAudio</c>), the choice
/// buttons (<c>UpdateChoices</c>), and short lines that aren't part of a conversation ("busy").
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

    public static void ShowChoices(List<string> choices)
    {
        var d = Dialogue._;
        if (d == null || d.optionButtons == null)
            return;
        d.GetComponent<GraphicRaycaster>().enabled = true;
        for (int i = 0; i < d.optionButtons.Count; i++)
        {
            var button = d.optionButtons[i];
            bool shown = i < choices.Count;
            button.gameObject.SetActive(shown);
            if (!shown)
                continue;
            if (i == 0 && EventSystem.current != null)
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
        }
    }

    private static void Show(Dialogue d, CharacterStories speaker, string text, bool isChoice)
    {
        d.storyText.SetText(text);
        if (_typing != null)
            d.StopCoroutine(_typing);
        var typing = d.storyText.GetComponent<TextAnimations>();
        _typing = typing != null ? d.StartCoroutine(typing.AnimateVertexColors(Dialogue.TextAnimationType.Simple)) : null;
        Voice(d, speaker, text, isChoice);
    }

    // Dialogue.PlayVoiceAudio's recipe: a blip every 0.1-0.2 s, about one per two words.
    private static void Voice(Dialogue d, CharacterStories speaker, string text, bool isChoice)
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
            _voice = isChoice || speaker == null ? PlayerVoice(d) : SpeakerVoice(d, speaker);
            _blipsLeft = 1 + Random.Range(0, 2) + Mathf.Max(0, Mathf.RoundToInt(Words(text) * 0.42f));
        }
        _nextBlip = Time.time;
    }

    private static AudioSource SpeakerVoice(Dialogue d, CharacterStories speaker)
    {
        if (speaker.TryGetComponent(out AudioSource own) && own.clip != null)
        {
            if (speaker.dialogueAudio != null)
                own.clip = speaker.dialogueAudio;
            return own;
        }
        return PlayerVoice(d);
    }

    private static AudioSource PlayerVoice(Dialogue d) => d.TryGetComponent(out AudioSource voice) ? voice : null;

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
