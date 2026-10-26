using System;
using System.Collections.Generic;
using IngameDebugConsole;
using NeetLoader;
using NeetLoader.Runtime;

namespace NeetModKit.Console;

/// <summary>
/// Adds commands to the game's built-in console (IngameDebugConsole). Commands are tied to the
/// mod that added them and removed when it unloads. Parameters are parsed by the console from the
/// delegate's signature (strings, numbers, bools, enums, vectors...).
/// </summary>
public static class ConsoleCommands
{
    private static readonly System.Text.RegularExpressions.Regex RichText = new System.Text.RegularExpressions.Regex("<[^>]+>");

    public static void Add(NeetMod owner, string command, string description, Action method) =>
        Register(owner, command, description, method);

    public static void Add<T1>(NeetMod owner, string command, string description, Action<T1> method) =>
        Register(owner, command, description, method);

    public static void Add<T1, T2>(NeetMod owner, string command, string description, Action<T1, T2> method) =>
        Register(owner, command, description, method);

    public static void Add<T1, T2, T3>(NeetMod owner, string command, string description, Action<T1, T2, T3> method) =>
        Register(owner, command, description, method);

    /// <summary>Runs a command line exactly as if it was typed into the console.</summary>
    public static void Execute(string commandLine) => DebugLogConsole.ExecuteCommand(commandLine);

    /// <summary>Names of every registered command (game, framework and mods).</summary>
    public static List<string> List()
    {
        var found = new List<ConsoleMethodInfo>();
        DebugLogConsole.FindCommands("", true, found);
        var names = new List<string>(found.Count);
        foreach (var info in found)
            names.Add(RichText.Replace(info.signature, ""));
        return names;
    }

    private static void Register(NeetMod owner, string command, string description, Delegate method)
    {
        DebugLogConsole.AddCommand(command, description, method);
        // Remove by delegate so same-named overloads from other mods (or the game) survive.
        owner.Track(Disposable.Create(() => DebugLogConsole.RemoveCommand(method)));
    }
}
