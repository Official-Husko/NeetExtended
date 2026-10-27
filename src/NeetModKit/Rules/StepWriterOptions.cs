using System.Collections.Generic;

namespace NeetModKit.Rules;

/// <summary>How <see cref="StepWriter"/> writes for one kind of content.</summary>
public sealed class StepWriterOptions
{
    /// <summary>Lines written where the knot ends, before "-> DONE" (a phone call ends on "# EndPhoneCall").</summary>
    public string[] EndLines = new string[0];

    /// <summary>
    /// Extra step types that say a line with a tag: step key -> tag. The phone has
    /// { "player", "SpeakerAudio:Player" } for lines the player says.
    /// </summary>
    public Dictionary<string, string> TaggedLines = new Dictionary<string, string>();

    /// <summary>How a choice is written: "+" (can be picked again when the knot is replayed) by default.</summary>
    public string ChoiceMark = "+";
}
