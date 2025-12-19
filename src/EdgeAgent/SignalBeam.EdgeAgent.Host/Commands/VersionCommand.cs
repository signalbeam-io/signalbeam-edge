using System.CommandLine;
using System.Reflection;

namespace SignalBeam.EdgeAgent.Host.Commands;

public static class VersionCommand
{
    public static Command Create()
    {
        var command = new Command("version", "Show the SignalBeam agent version");

        command.SetHandler(() =>
        {
            Execute();
            return Task.FromResult(0);
        });

        return command;
    }

    private static void Execute()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? version?.ToString() ?? "unknown";

        Console.WriteLine($"SignalBeam Edge Agent v{informationalVersion}");
        Console.WriteLine();
        Console.WriteLine($"Runtime: {Environment.Version}");
        Console.WriteLine($"Platform: {Environment.OSVersion.Platform}");
    }
}
