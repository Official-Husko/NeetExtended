using System.Collections.Generic;
using Ink.Runtime;

namespace NeetModKit.Conversations;

/// <summary>A mod's Ink script added to one of the game's stories. Created by <see cref="InkScripts.Add(NeetLoader.Runtime.ITracker, InkStory, string, string)"/>.</summary>
public sealed class InkScript
{
    internal readonly List<string> ProblemList = new List<string>();
    internal readonly List<KeyValuePair<string, INamedContent>> Grafted = new List<KeyValuePair<string, INamedContent>>();

    internal InkScript(InkStory target, string name, string source)
    {
        Target = target;
        Name = name;
        Source = source;
    }

    /// <summary>The story the script goes into.</summary>
    public InkStory Target { get; }

    /// <summary>Shown in errors, usually the file it came from.</summary>
    public string Name { get; }

    public string Source { get; }

    /// <summary>Top-level knots and functions the script adds. Known after it first compiled.</summary>
    public IReadOnlyList<string> Knots => KnotList;

    /// <summary>Compiler errors and warnings, with line numbers counted in <see cref="Source"/>.</summary>
    public IReadOnlyList<string> Problems => ProblemList;

    /// <summary>True when the script couldn't be compiled or added (see <see cref="Problems"/>).</summary>
    public bool Failed { get; internal set; }

    /// <summary>True once it compiled; scripts compile when the game first creates their story.</summary>
    public bool Compiled => Json != null;

    internal string Json { get; set; }
    internal List<string> KnotList { get; } = new List<string>();
    internal Story GraftedInto { get; set; }
}
