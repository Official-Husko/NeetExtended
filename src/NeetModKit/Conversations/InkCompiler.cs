using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ink;
using Ink.Runtime;

namespace NeetModKit.Conversations;

/// <summary>
/// Compiles mod scripts with the Ink compiler the game ships. Each script gets a hidden header that
/// declares the game's lists, global variables and every known function, so scripts can use them
/// without declaring anything themselves.
/// </summary>
internal static class InkCompiler
{
    private static readonly Regex LineNumber = new Regex(@"line (\d+)");

    public sealed class Result
    {
        public string Json;
        public readonly List<string> Problems = new List<string>();
        public bool Ok => Json != null;
    }

    public static Result Compile(Story story, string name, string source)
    {
        string header = Header(story);
        int headerLines = CountLines(header);
        var result = new Result();
        bool hadError = false;

        var options = new Compiler.Options
        {
            sourceFilename = name,
            countAllVisits = true,
            errorHandler = (message, type) =>
            {
                if (type == ErrorType.Error)
                    hadError = true;
                result.Problems.Add(FixLineNumber(message, headerLines));
            },
        };

        try
        {
            var compiled = new Compiler(header + source, options).Compile();
            if (compiled != null && !hadError)
                result.Json = compiled.ToJson();
        }
        catch (Exception e)
        {
            result.Problems.Add("ERROR: " + e.Message);
        }

        if (!result.Ok && result.Problems.Count == 0)
            result.Problems.Add("ERROR: the script didn't compile.");
        return result;
    }

    private static string Header(Story story)
    {
        var sb = new StringBuilder();
        var listNames = new HashSet<string>();
        foreach (var list in story.listDefinitions.lists)
        {
            listNames.Add(list.name);
            sb.Append("LIST ").Append(list.name).Append(" = ");
            bool first = true;
            foreach (var item in list.items.Keys)
            {
                if (!first)
                    sb.Append(", ");
                sb.Append(item.itemName);
                first = false;
            }
            sb.Append('\n');
        }

        // Only the names and types matter here: these declarations never run in the game's story.
        foreach (string variable in story.variablesState)
        {
            if (listNames.Contains(variable))
                continue;
            string literal = Literal(story.variablesState[variable]);
            if (literal != null)
                sb.Append("VAR ").Append(variable).Append(" = ").Append(literal).Append('\n');
        }

        foreach (var function in InkFunctions.Declarations())
            sb.Append("EXTERNAL ").Append(function.Key).Append('(').Append(string.Join(", ", function.Value)).Append(")\n");
        return sb.ToString();
    }

    private static string Literal(object value)
    {
        switch (value)
        {
            case int _: return "0";
            case float _: return "0.0";
            case bool _: return "false";
            case string _: return "\"\"";
            case InkList _: return "()";
            default: return null;
        }
    }

    private static int CountLines(string text)
    {
        int lines = 0;
        foreach (char c in text)
        {
            if (c == '\n')
                lines++;
        }
        return lines;
    }

    private static string FixLineNumber(string message, int headerLines) =>
        LineNumber.Replace(message, m =>
            "line " + Math.Max(1, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) - headerLines).ToString(CultureInfo.InvariantCulture));
}
