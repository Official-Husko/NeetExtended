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

    private static string _toast;
    private static float _toastUntil;

    public static void Clear()
    {
        _typing = null;
        TalkVoice.Clear();
        _toast = null;
    }

    /// <summary>The talker's own line: the game's typing effect and the blips rolled from the line's seed.</summary>
    public static void ShowLine(CharacterStories speaker, string text, bool isChoice, int seed)
    {
        var d = Dialogue._;
        if (d == null || d.storyText == null)
            return;
        _toast = null;
        d.storyText.enabled = true;
        d.GetComponent<CanvasGroup>().interactable = true;
        HideChoices();
        Show(d, speaker, text, isChoice, seed);
    }

    /// <summary>A line that isn't a conversation, gone after a moment unless a conversation takes the box.</summary>
    public static void Toast(CharacterStories speaker, string text)
    {
        var d = Dialogue._;
        if (d == null || d.storyText == null || d.currSpeaker != null)
            return;
        d.storyText.enabled = true;
        Show(d, speaker, text, false, LineVoice.NewSeed());
        _toast = text;
        _toastUntil = Time.time + ToastTime;
    }

    /// <summary>Another player's line, in a box nobody can click. With a talker the voice plays too, theirs from their body.</summary>
    public static void ShowWatched(CharacterStories speaker, string text, bool playerVoice, Transform talker, bool voice, int seed)
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
            TalkVoice.Begin(d, speaker, text, playerVoice, talker, seed);
        else
            TalkVoice.Stop();
    }

    /// <summary>Takes another player's line away.</summary>
    public static void HideWatched()
    {
        var d = Dialogue._;
        if (d == null || d.storyText == null)
            return;
        TalkVoice.Stop();
        if (_typing != null)
        {
            d.StopCoroutine(_typing);
            _typing = null;
        }
        d.storyText.SetText("");
        d.storyText.enabled = false;
        HideChoices();
    }

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
        TalkVoice.Tick();
        float now = Time.time;
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

    private static void Show(Dialogue d, CharacterStories speaker, string text, bool isChoice, int seed)
    {
        d.storyText.SetText(text);
        Type(d);
        TalkVoice.Begin(d, speaker, text, isChoice, null, seed);
    }

    private static void Type(Dialogue d)
    {
        if (_typing != null)
            d.StopCoroutine(_typing);
        var typing = d.storyText.GetComponent<TextAnimations>();
        _typing = typing != null ? d.StartCoroutine(typing.AnimateVertexColors(Dialogue.TextAnimationType.Simple)) : null;
    }
}
