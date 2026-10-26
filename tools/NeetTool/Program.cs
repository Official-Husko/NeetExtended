using System.Reflection;
using NeetTool.Bridge;
using NeetTool.Cli;

var commands = Assembly.GetExecutingAssembly().GetTypes()
    .Where(t => typeof(ICommand).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
    .Select(t => (ICommand)Activator.CreateInstance(t)!)
    .OrderBy(c => c.Name)
    .ToList();

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine("neet - NEET Demo modding tool\n");
    foreach (var c in commands)
        Console.WriteLine($"  {c.Usage,-50} {c.Summary}");
    Console.WriteLine("\nCommon options: --game DIR (game folder), --port N (bridge port, default 27850)");
    return 0;
}

var command = commands.FirstOrDefault(c => c.Name == args[0]);
if (command is null)
{
    Out.Error($"Unknown command '{args[0]}'. Run 'neet help'.");
    return 2;
}

try
{
    return await command.RunAsync(new Args(args.Skip(1)));
}
catch (Exception e) when (e is BridgeException or InvalidOperationException or ArgumentException or FileNotFoundException)
{
    Out.Error(e.Message);
    return 1;
}
