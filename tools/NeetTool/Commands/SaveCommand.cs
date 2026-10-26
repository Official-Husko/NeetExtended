using System.Text.Json;
using System.Text.Json.Nodes;
using NeetModKit.Saves;
using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class SaveCommand : ICommand
{
    public string Name => "save";
    public string Usage => "save list | info FILE | decode FILE [OUT] | encode FILE [OUT]";
    public string Summary => "Inspect and convert save files (.nsav <-> readable JSON)";

    public Task<int> RunAsync(Args args)
    {
        string action = args.At(0) ?? "list";
        return Task.FromResult(action switch
        {
            "list" => List(args),
            "info" => Info(Require(args, 1)),
            "decode" => Decode(Require(args, 1), args.At(2)),
            "encode" => Encode(Require(args, 1), args.At(2)),
            _ => throw new ArgumentException("Usage: neet " + Usage),
        });
    }

    private static int List(Args args)
    {
        string? folder = GameInstall.Locate(args.Get("game")).PersistentDataDir();
        if (folder is null || !Directory.Exists(folder))
        {
            Out.Error("No save folder yet. Play the game once first.");
            return 1;
        }

        Out.Info(folder);
        foreach (var file in new DirectoryInfo(folder).GetFiles("neet*").OrderByDescending(f => f.LastWriteTime))
        {
            var info = SaveCodec.Inspect(File.ReadAllBytes(file.FullName));
            string kind = info.IsContainer ? $"nsav v{info.Version} {info.Codec}" : "plain JSON";
            Out.Info($"  {file.LastWriteTime:yyyy-MM-dd HH:mm}  {file.Length / 1024,6} KB  {kind,-18} {file.Name}");
        }
        return 0;
    }

    private static int Info(string path)
    {
        var info = SaveCodec.Inspect(File.ReadAllBytes(path));
        Out.Info($"File:       {path}");
        Out.Info($"Format:     {(info.IsContainer ? $".nsav version {info.Version}, codec {info.Codec}" : "plain JSON (original game format)")}");
        Out.Info($"Size:       {info.FileLength:N0} bytes on disk, {info.JsonLength:N0} bytes of JSON");
        if (info.IsContainer)
            Out.Info($"Checksum:   {info.Crc:X8}");

        var save = JsonNode.Parse(SaveCodec.Decode(File.ReadAllBytes(path)))!;
        var entries = save["Save"]!.AsArray();
        var types = entries.GroupBy(e => e?["type"]?.GetValue<string>() ?? "?").OrderByDescending(g => g.Count());
        Out.Info($"Entries:    {entries.Count} ({string.Join(", ", types.Take(8).Select(g => $"{g.Key} x{g.Count()}"))}...)");
        return 0;
    }

    private static int Decode(string path, string? output)
    {
        output ??= Path.ChangeExtension(path, ".json");
        if (Path.GetFullPath(output) == Path.GetFullPath(path))
            throw new ArgumentException("Pick a different output file.");

        var json = JsonNode.Parse(SaveCodec.Decode(File.ReadAllBytes(path)));
        File.WriteAllText(output, json!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Out.Ok($"Wrote readable JSON to {output}");
        return 0;
    }

    private static int Encode(string path, string? output)
    {
        output ??= Path.ChangeExtension(path, SaveCodec.Extension);
        // Parse first so a typo in hand-edited JSON is caught here and not by the game.
        var json = JsonNode.Parse(File.ReadAllText(path))!.ToJsonString();
        File.WriteAllBytes(output, SaveCodec.Encode(json));
        Out.Ok($"Wrote {output} ({new FileInfo(output).Length / 1024} KB)");
        return 0;
    }

    private static string Require(Args args, int index) =>
        args.At(index) ?? throw new ArgumentException("Usage: neet save " + "list | info FILE | decode FILE [OUT] | encode FILE [OUT]");
}
