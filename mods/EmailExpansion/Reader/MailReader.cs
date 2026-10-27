using System;
using System.Collections.Generic;
using System.Globalization;
using EmailExpansion.Mail;
using NeetModKit.Events;
using NeetModKit.Game;
using Newtonsoft.Json.Linq;

namespace EmailExpansion.Reader;

/// <summary>
/// Shows modded emails in the inbox. Opening and replying are actions that run where the house runs
/// (<see cref="SharedLetters"/>, <see cref="LetterRunner"/>); the reader shows the conversation as it
/// was published, so in co-op every player sees the same one and anyone can answer it.
/// </summary>
internal static class MailReader
{
    private static EmailExpansionMod _mod;
    private static ReaderView _view;
    private static bool _loading;

    // What the reader shows now, to refresh it when the conversation moves on.
    private static EmailApp _app;
    private static MailItem _mail;
    private static string _key;

    /// <summary>The email being run right now, for its conditions and actions; null otherwise.</summary>
    public static MailItem Active => LetterRunner.Active;

    public static void Init(EmailExpansionMod mod)
    {
        _mod = mod;
        LetterRunner.Init(mod);
        SharedLetters.Init(mod);
        SharedState.Changed.Subscribe(mod, OnChanged);
        // While a save loads, the game reopens every read email to rebuild the inbox. Nothing of
        // ours is restored yet then, so those opens are left alone.
        GameEvents.BeforeLoad.Subscribe(mod, () => _loading = true);
        GameEvents.AfterLoad.Subscribe(mod, () => _loading = false);
    }

    /// <summary>Runs in place of EmailApp.LoadEmail. False when it showed the email itself.</summary>
    public static bool Open(EmailApp app, KnotList.Knot knot)
    {
        var mail = knot != null ? MailBook.ByKnot(knot.inkName) : null;
        if (mail == null)
        {
            _key = null;
            _view?.Hide();
            return true; // One of the game's.
        }
        if (_loading)
            return false;

        string key = Key(knot);
        MailMemory.RecordRead(mail.Id);
        _app = app;
        _mail = mail;
        _key = null;
        // Where the house runs this catches the letter up right away; elsewhere it asks the host,
        // and the reader fills in when the answer arrives.
        if (WorldRole.RunsWorld || !SharedLetters.TryGet(key, out _))
            SharedLetters.Open(key, mail);
        _key = key;
        Show(toBottom: false);
        return false;
    }

    private static void Show(bool toBottom)
    {
        if (_app == null || _key == null)
            return;
        _view = ReaderView.For(_mod, _app, _view);
        if (!SharedLetters.TryGet(_key, out var letter))
        {
            _view.Show("...", Array.Empty<string>(), null, toBottom: false);
            return;
        }
        string key = _key;
        var mail = _mail;
        _view.Show(letter.Text, letter.Done ? Array.Empty<string>() : (IReadOnlyList<string>)letter.Choices,
            index => SharedLetters.Reply(key, mail, index), toBottom);
    }

    private static void OnChanged(KeyValuePair<string, JToken> change)
    {
        if (_key != null && _view != null && _view.IsShowing && SharedLetters.IsLetter(change.Key, out string key) && key == _key)
            Show(toBottom: true);
    }

    /// <summary>One inbox entry: the same email can be in the inbox more than once.</summary>
    private static string Key(KnotList.Knot knot) =>
        knot.inkName + "@" + knot.timeStamp.ToString("R", CultureInfo.InvariantCulture);
}
