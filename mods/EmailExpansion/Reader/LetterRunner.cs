using System;
using System.Collections.Generic;
using System.Text;
using EmailExpansion.Mail;
using Ink.Runtime;
using NeetModKit.Conversations;
using NeetModKit.Rules;

namespace EmailExpansion.Reader;

/// <summary>
/// Runs modded emails where the house runs. The game reads one line of an email's knot; a modded
/// email is read to the end or to its replies, with its actions running the first time. Each inbox
/// entry runs in its own Ink flow, so opening other emails doesn't lose its place, and keeps its text
/// and the replies picked in the save. After loading a save, the flow is rebuilt by replaying those
/// replies with the actions switched off. Every change is published (<see cref="SharedLetters"/>).
/// </summary>
internal static class LetterRunner
{
    private const string FlowPrefix = "NeetMail_";
    private const string Paragraph = "\n\n";

    private static readonly HashSet<string> LiveFlows = new HashSet<string>();
    private static readonly List<string> Choices = new List<string>();
    private static readonly StringBuilder Lines = new StringBuilder();
    private static EmailExpansionMod _mod;

    /// <summary>The email being run right now, for its conditions and actions; null otherwise.</summary>
    public static MailItem Active { get; private set; }

    public static void Init(EmailExpansionMod mod)
    {
        _mod = mod;
        InkStory.Email.Created.Subscribe(mod, _ => LiveFlows.Clear());
    }

    /// <summary>Brings an inbox entry up to date (the first time, that reads it and runs its actions).</summary>
    public static void Open(string key, MailItem mail)
    {
        var letter = MailMemory.LetterFor(key);
        var story = InkStory.Email.Current;
        Choices.Clear();
        if (story != null && !letter.Done)
            Run(story, key, mail, letter, () => Catch(story, mail, letter));
        SharedLetters.Publish(key, letter, Choices);
    }

    public static void Reply(string key, MailItem mail, int index)
    {
        var letter = MailMemory.LetterFor(key);
        var story = InkStory.Email.Current;
        if (story == null || letter.Done)
            return;
        Choices.Clear();
        Run(story, key, mail, letter, () =>
        {
            Catch(story, mail, letter);
            if (index < 0 || index >= story.currentChoices.Count)
                return;
            string reply = story.currentChoices[index].text;
            story.ChooseChoiceIndex(index);
            letter.Replies.Add(reply);
            letter.Text += Paragraph + "<b>You:</b> " + reply;
            string more = ReadOn(story);
            if (more.Length > 0)
                letter.Text += Paragraph + more;
            Settle(story, letter);
        });
        SharedLetters.Publish(key, letter, Choices);
    }

    /// <summary>Gets the flow to where the letter is: started fresh, or replayed after a load.</summary>
    private static void Catch(Story story, MailItem mail, MailMemory.Letter letter)
    {
        string flow = story.currentFlowName;
        if (!LiveFlows.Add(flow))
        {
            Settle(story, letter);
            return;
        }
        story.ChoosePathString(mail.Knot);
        if (!letter.Started)
        {
            letter.Started = true;
            letter.Text = ReadOn(story);
        }
        else
        {
            Replay(story, letter);
        }
        Settle(story, letter);
    }

    private static void Replay(Story story, MailMemory.Letter letter)
    {
        bool wasMuted = RuleSteps.Muted;
        RuleSteps.Muted = true;
        try
        {
            ReadOn(story);
            foreach (string reply in letter.Replies)
            {
                int index = IndexOf(story, reply);
                if (index < 0)
                    break; // Something changed since; leave it at this reply.
                story.ChooseChoiceIndex(index);
                ReadOn(story);
            }
        }
        finally
        {
            RuleSteps.Muted = wasMuted;
        }
    }

    /// <summary>Notes the replies on offer, or that the email is finished.</summary>
    private static void Settle(Story story, MailMemory.Letter letter)
    {
        Choices.Clear();
        foreach (var choice in story.currentChoices)
            Choices.Add(choice.text);
        if (Choices.Count == 0 && !story.canContinue)
            letter.Done = true;
    }

    private static void Run(Story story, string key, MailItem mail, MailMemory.Letter letter, Action body)
    {
        var previous = RuleContext.Active;
        Active = mail;
        RuleContext.Active = mail;
        try
        {
            story.SwitchFlow(FlowPrefix + key);
            body();
        }
        catch (Exception e)
        {
            _mod.Log.Warn($"Email {mail.Id} stopped: {e.Message}");
            letter.Done = true;
            Choices.Clear();
        }
        finally
        {
            story.SwitchToDefaultFlow();
            Active = null;
            RuleContext.Active = previous;
        }
    }

    /// <summary>Everything up to the next replies or the end, one paragraph per line.</summary>
    private static string ReadOn(Story story)
    {
        Lines.Clear();
        while (story.canContinue)
        {
            string line = story.Continue().Trim();
            if (line.Length == 0)
                continue;
            if (Lines.Length > 0)
                Lines.Append(Paragraph);
            // The game's own emails mark paragraphs and line breaks like this, often with a space after.
            Lines.Append(line.Replace("^N ", Paragraph).Replace("^N", Paragraph).Replace("^n ", "\n").Replace("^n", "\n"));
        }
        return Lines.ToString();
    }

    private static int IndexOf(Story story, string text)
    {
        var choices = story.currentChoices;
        for (int i = 0; i < choices.Count; i++)
        {
            if (choices[i].text == text)
                return i;
        }
        return -1;
    }
}
