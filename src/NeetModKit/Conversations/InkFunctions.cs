using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Ink.Runtime;
using NeetLoader;
using NeetLoader.Logging;
using NeetLoader.Runtime;

namespace NeetModKit.Conversations;

/// <summary>
/// Functions Ink scripts can call, like the game's own GiveItem or KnockoutPlayer. Registered
/// functions are bound to every copy of every game story (<see cref="InkStory"/>) and declared for
/// <see cref="InkScripts"/> automatically, so scripts just call them: <c>~ MyFunction("x", 2)</c>.
/// </summary>
public static class InkFunctions
{
    private sealed class Entry
    {
        public string Name;
        public string[] Parameters;
        public Func<object[], object> Body;
        public bool LookaheadSafe;
    }

    private static readonly Regex ValidName = new Regex("^[A-Za-z_][A-Za-z0-9_]*$");
    private static readonly FieldInfo ExternalsField = AccessTools.Field(typeof(Story), "_externals");
    private static readonly Dictionary<string, Entry> Registered = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private static ModLogger _log;

    /// <summary>
    /// Registers <paramref name="name"/> with the given parameter names. The body gets the arguments as
    /// Ink passed them (see <see cref="InkValues"/>) and returns a value, or null for none.
    /// Set <paramref name="lookaheadSafe"/> only for functions without side effects: Ink may then call
    /// them early while it looks ahead, and more than once.
    /// </summary>
    public static void Register(ITracker owner, string name, string[] parameters, Func<object[], object> body, bool lookaheadSafe = false)
    {
        if (name == null || !ValidName.IsMatch(name))
            throw new ArgumentException($"'{name}' isn't a valid Ink function name.");
        if (GameInkFunctions.Contains(name))
            throw new ArgumentException($"'{name}' is one of the game's own functions.");
        if (Registered.ContainsKey(name))
            throw new ArgumentException($"A dialogue function called '{name}' is already registered.");

        var entry = new Entry { Name = name, Parameters = parameters ?? new string[0], Body = body, LookaheadSafe = lookaheadSafe };
        Registered[name] = entry;
        foreach (var story in InkStory.All)
            Bind(story.Current, entry);

        owner.Track(Disposable.Create(() =>
        {
            if (!Registered.TryGetValue(name, out var current) || current != entry)
                return;
            Registered.Remove(name);
            foreach (var story in InkStory.All)
                BindStub(story.Current, name);
        }));
    }

    /// <summary>Every function dialogue scripts can call: the game's and the registered ones, with parameter names.</summary>
    public static IEnumerable<KeyValuePair<string, string[]>> Declarations() => Declarations(InkStory.Dialogue);

    /// <summary>Every function scripts in <paramref name="story"/> can call: the ones the game binds to it and the registered ones.</summary>
    public static IEnumerable<KeyValuePair<string, string[]>> Declarations(InkStory story)
    {
        foreach (var pair in story.GameFunctions)
            yield return pair;
        foreach (var entry in Registered.Values)
            yield return new KeyValuePair<string, string[]>(entry.Name, entry.Parameters);
    }

    internal static void Init(NeetMod kit)
    {
        _log = kit.Log;
        foreach (var story in InkStory.All)
            story.WhenCreated(kit, BindAll);
    }

    private static void BindAll(Story story)
    {
        foreach (var entry in Registered.Values)
            Bind(story, entry);
    }

    private static void Bind(Story story, Entry entry)
    {
        if (story == null)
            return;
        Unbind(story, entry.Name);
        string name = entry.Name;
        var body = entry.Body;
        story.BindExternalFunctionGeneral(name, args => Invoke(name, body, args), entry.LookaheadSafe);
    }

    /// <summary>
    /// Scripts that call a function stay in the story after the function's mod unloads (until they
    /// are removed too), so the name keeps answering instead of stopping the conversation.
    /// </summary>
    private static void BindStub(Story story, string name)
    {
        if (story == null)
            return;
        Unbind(story, name);
        story.BindExternalFunctionGeneral(name, _ => 0, true);
    }

    private static void Unbind(Story story, string name)
    {
        // A previous copy of the framework (hot reload) may have bound the name already.
        if (ExternalsField.GetValue(story) is IDictionary bound && bound.Contains(name))
            story.UnbindExternalFunction(name);
    }

    private static object Invoke(string name, Func<object[], object> body, object[] args)
    {
        try
        {
            return body(args);
        }
        catch (Exception e)
        {
            _log.Error($"Dialogue function {name} failed", e);
            return 0;
        }
    }
}
