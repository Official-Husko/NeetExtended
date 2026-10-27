using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Rules;

/// <summary>
/// Turns a list of steps from a content file into an Ink knot: lines, and objects with choice,
/// choices, if, random, do, end, goto, label or ink. Every branch (a choice, an if, a random pick)
/// gets its own stitch that ends by going on to whatever follows, so nesting never gets deep and the
/// result reads like a flat script. Conditions and actions are registered with <see cref="RuleSteps"/>
/// through the callbacks and called as RuleIf(n) and RuleDo(n).
/// </summary>
public sealed class StepWriter
{
    private const string TheEnd = "the_end";

    private sealed class Block
    {
        public string Name;
        public JArray Steps;
        public int Start;
        public string Next;
        public string Where;
    }

    private readonly StringBuilder _ink = new StringBuilder();
    private readonly Queue<Block> _pending = new Queue<Block>();
    private readonly HashSet<string> _labels = new HashSet<string>();
    private readonly Func<JObject, int> _addAction;
    private readonly Func<JToken, int> _addCondition;
    private readonly StepWriterOptions _options;
    private readonly string _stepsHelp;
    private int _blocks;

    private StepWriter(Func<JObject, int> addAction, Func<JToken, int> addCondition, StepWriterOptions options)
    {
        _addAction = addAction;
        _addCondition = addCondition;
        _options = options ?? new StepWriterOptions();
        var kinds = new List<string> { "choice", "choices", "if", "random", "do" };
        kinds.AddRange(_options.TaggedLines.Keys);
        kinds.AddRange(new[] { "end", "goto", "label", "ink" });
        _stepsHelp = "lines, or objects with " + string.Join(", ", kinds);
    }

    /// <summary>
    /// Writes knot <paramref name="knot"/> from <paramref name="steps"/>. <paramref name="where"/> names the
    /// list in error messages ("say"). Throws <see cref="FormatException"/> pointing at the step that's wrong.
    /// </summary>
    public static string Write(string knot, JToken steps, Func<JObject, int> addAction, Func<JToken, int> addCondition,
        StepWriterOptions options = null, string where = "say")
    {
        var writer = new StepWriter(addAction, addCondition, options);
        var list = AsSteps(steps);
        writer.CollectLabels(list, where);

        writer._ink.Append("=== ").Append(knot).Append(" ===\n");
        writer.WriteBlock(list, 0, TheEnd, where);
        while (writer._pending.Count > 0)
        {
            var block = writer._pending.Dequeue();
            writer._ink.Append("= ").Append(block.Name).Append('\n');
            writer.WriteBlock(block.Steps, block.Start, block.Next, block.Where);
        }
        writer._ink.Append("= ").Append(TheEnd).Append('\n');
        foreach (string line in writer._options.EndLines)
            writer._ink.Append(line).Append('\n');
        writer._ink.Append("-> DONE\n");
        return writer._ink.ToString();
    }

    private void WriteBlock(JArray steps, int start, string next, string where)
    {
        for (int i = start; i < steps.Count; i++)
        {
            var step = steps[i];
            string here = $"{where}[{i}]";
            switch (step.Type)
            {
                case JTokenType.String:
                    Say(step.Value<string>(), null);
                    break;
                case JTokenType.Array:
                    WriteBlock((JArray)step, 0, Rest(steps, i + 1, next, where), here);
                    return;
                case JTokenType.Object:
                    if (WriteStep((JObject)step, steps, i, next, where, here))
                        return;
                    break;
                default:
                    throw new FormatException($"{here}: expected {_stepsHelp}.");
            }
        }
        Line("-> " + next);
    }

    /// <summary>Writes one object step. Returns true if it ended the block (it went somewhere else).</summary>
    private bool WriteStep(JObject step, JArray steps, int index, string next, string where, string here)
    {
        if (step["choice"] != null || step["choices"] != null)
        {
            WriteChoices(step, Rest(steps, index + 1, next, where), here);
            return true;
        }
        if (step["if"] != null)
        {
            string rest = Rest(steps, index + 1, next, where);
            int condition = Condition(step["if"], here + ".if");
            string yes = Branch(step["then"], rest, here + ".then");
            string no = Branch(step["else"], rest, here + ".else");
            Line($"{{RuleIf({condition}): -> {yes} | -> {no}}}");
            return true;
        }
        if (step["random"] != null)
        {
            if (!(step["random"] is JArray options) || options.Count == 0)
                throw new FormatException($"{here}.random: needs a list of options.");
            string rest = Rest(steps, index + 1, next, where);
            var sb = new StringBuilder("{~");
            for (int i = 0; i < options.Count; i++)
                sb.Append(i == 0 ? " -> " : " | -> ").Append(Branch(options[i], rest, $"{here}.random[{i}]"));
            Line(sb.Append(" }").ToString());
            return true;
        }
        if (step["do"] != null)
        {
            Line($"~ RuleDo({Action(step, here)})");
            return false;
        }
        foreach (var tagged in _options.TaggedLines)
        {
            if (step[tagged.Key] != null)
            {
                Say(step[tagged.Key].Value<string>(), tagged.Value);
                return false;
            }
        }
        if (step["end"] != null)
        {
            Line("-> " + TheEnd);
            return true;
        }
        if (step["goto"] != null)
        {
            string label = Label(step["goto"], here);
            if (!_labels.Contains(label))
                throw new FormatException($"{here}: there's no label \"{step["goto"]}\" here.");
            Line("-> " + label);
            return true;
        }
        if (step["label"] != null)
        {
            string label = Label(step["label"], here);
            Line("-> " + label);
            _pending.Enqueue(new Block { Name = label, Steps = steps, Start = index + 1, Next = next, Where = where });
            return true;
        }
        if (step["ink"] != null)
        {
            foreach (var line in AsSteps(step["ink"]))
                Line(line.Value<string>());
            return false;
        }
        throw new FormatException($"{here}: don't know what to do with {step.ToString(Newtonsoft.Json.Formatting.None)}. Steps are {_stepsHelp}.");
    }

    private void WriteChoices(JObject step, string rest, string where)
    {
        bool hidden = false;
        int count = 0;

        if (step["choice"] is JObject map)
        {
            foreach (var option in map.Properties())
                hidden |= Choice(option.Name, option.Value, null, false, rest, $"{where}.choice.{option.Name}", ref count);
        }
        else if (step["choices"] is JArray list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                string here = $"{where}.choices[{i}]";
                if (!(list[i] is JObject option) || option["text"] == null)
                    throw new FormatException($"{here}: each choice needs a \"text\".");
                var then = option["say"] ?? option["then"];
                hidden |= Choice(option["text"].Value<string>(), then, option["if"], option.Value<bool?>("once") ?? false, rest, here, ref count);
            }
        }
        else
        {
            throw new FormatException($"{where}: \"choice\" takes an object (text: what follows), \"choices\" a list.");
        }

        if (count == 0)
            throw new FormatException($"{where}: there are no choices.");
        // If every choice can be hidden or used up, carry on instead of getting stuck.
        if (hidden)
            Line("+ -> " + rest);
    }

    private bool Choice(string text, JToken then, JToken condition, bool once, string rest, string where, ref int count)
    {
        string escaped = Text(text, where);
        if (escaped.Length == 0)
            throw new FormatException($"{where}: a choice needs some text.");
        string test = condition != null ? $" {{RuleIf({Condition(condition, where + ".if")})}}" : "";
        // Ink reads a choice that starts with "{...}" as another condition, so text with values
        // shows in brackets and, when choices are said, is said as the branch's first line instead.
        bool values = RuleTexts.HasValues(text);
        string shown = _options.SayChoices && !values ? escaped : "[" + escaped + "]";
        Line($"{(once ? "*" : _options.ChoiceMark)}{test} {shown}");
        if (_options.SayChoices && values)
            Line("  " + escaped);
        Line("  -> " + Branch(then, rest, where));
        count++;
        return condition != null || once;
    }

    /// <summary>Queues steps as their own stitch and returns its name, or <paramref name="next"/> if there are none.</summary>
    private string Branch(JToken steps, string next, string where)
    {
        if (steps == null || steps.Type == JTokenType.Null)
            return next;
        var list = AsSteps(steps);
        if (list.Count == 0)
            return next;
        string name = "s" + ++_blocks;
        _pending.Enqueue(new Block { Name = name, Steps = list, Start = 0, Next = next, Where = where });
        return name;
    }

    /// <summary>The steps after a branch, as their own stitch (or <paramref name="next"/> if nothing follows).</summary>
    private string Rest(JArray steps, int from, string next, string where)
    {
        if (from >= steps.Count)
            return next;
        string name = "s" + ++_blocks;
        _pending.Enqueue(new Block { Name = name, Steps = steps, Start = from, Next = next, Where = where });
        return name;
    }

    private void Say(string text, string tag)
    {
        foreach (string raw in (text ?? "").Split('\n'))
        {
            string line = Text(raw, "a line");
            if (line.Length == 0)
                continue;
            Line(tag == null ? line : line + " # " + tag);
        }
    }

    // Plain text with {{values}} (RuleTexts) as Ink.
    private static string Text(string text, string where)
    {
        try { return RuleTexts.ToInk(text ?? ""); }
        catch (FormatException e) { throw new FormatException($"{where}: {e.Message}"); }
    }

    private int Condition(JToken condition, string where)
    {
        try { return _addCondition(condition); }
        catch (FormatException e) { throw new FormatException($"{where}: {e.Message}"); }
    }

    private int Action(JObject step, string where)
    {
        try { return _addAction(step); }
        catch (FormatException e) { throw new FormatException($"{where}: {e.Message}"); }
    }

    private void CollectLabels(JToken token, string where)
    {
        if (token is JArray array)
        {
            for (int i = 0; i < array.Count; i++)
                CollectLabels(array[i], $"{where}[{i}]");
        }
        else if (token is JObject obj)
        {
            if (obj["label"] != null && !_labels.Add(Label(obj["label"], where)))
                throw new FormatException($"{where}: the label \"{obj["label"]}\" is used twice.");
            foreach (var property in obj.Properties())
                CollectLabels(property.Value, where);
        }
    }

    private static string Label(JToken name, string where)
    {
        string text = name.Type == JTokenType.String ? name.Value<string>() : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException($"{where}: labels need a name.");
        return "l_" + InkText.Identifier(text);
    }

    private static JArray AsSteps(JToken token) => token as JArray ?? new JArray(token);

    private void Line(string line) => _ink.Append(line).Append('\n');
}
