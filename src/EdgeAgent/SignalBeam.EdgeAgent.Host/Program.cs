using System.CommandLine;
using SignalBeam.EdgeAgent.Host.Commands;

namespace SignalBeam.EdgeAgent.Host;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("SignalBeam Edge Agent - Manage your edge devices from the cloud");

        // Add all commands
        rootCommand.AddCommand(RegisterCommand.Create());
        rootCommand.AddCommand(RunCommand.Create());
        rootCommand.AddCommand(StatusCommand.Create());
        rootCommand.AddCommand(VersionCommand.Create());
        rootCommand.AddCommand(LogsCommand.Create());

        return await rootCommand.InvokeAsync(args);
    }
}
