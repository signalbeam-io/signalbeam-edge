using System.CommandLine;
using SignalBeam.EdgeAgent.Host.Commands;

namespace SignalBeam.EdgeAgent.Host;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("SignalBeam Edge Agent - Manage your edge devices from the cloud");

        // Add all commands
        rootCommand.Subcommands.Add(RegisterCommand.Create());
        rootCommand.Subcommands.Add(RunCommand.Create());
        rootCommand.Subcommands.Add(RequestCertCommand.Create());
        rootCommand.Subcommands.Add(StatusCommand.Create());
        rootCommand.Subcommands.Add(VersionCommand.Create());
        rootCommand.Subcommands.Add(LogsCommand.Create());

        return await rootCommand.Parse(args).InvokeAsync();
    }
}
