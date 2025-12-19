using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SignalBeam.EdgeAgent.Host.Services;

namespace SignalBeam.EdgeAgent.Host.Commands;

public static class StatusCommand
{
    public static Command Create()
    {
        var command = new Command("status", "Show the current status of the SignalBeam agent");

        command.SetHandler(() =>
        {
            Execute();
            return Task.FromResult(0);
        });

        return command;
    }

    private static int Execute()
    {
        var serviceProvider = HostBuilder.BuildServiceProvider();
        var stateManager = serviceProvider.GetRequiredService<DeviceStateManager>();

        Console.WriteLine("SignalBeam Edge Agent Status");
        Console.WriteLine("============================");
        Console.WriteLine();

        if (!stateManager.IsRegistered)
        {
            Console.WriteLine("Status: ❌ Not Registered");
            Console.WriteLine();
            Console.WriteLine("Run 'signalbeam-agent register' to register this device.");
            return 1;
        }

        Console.WriteLine("Status: ✅ Registered");
        Console.WriteLine($"Device ID: {stateManager.DeviceId}");
        Console.WriteLine($"Cloud Endpoint: {stateManager.CloudEndpoint}");
        Console.WriteLine();
        Console.WriteLine($"Machine: {Environment.MachineName}");
        Console.WriteLine($"Platform: {Environment.OSVersion.Platform}");
        Console.WriteLine($"OS Version: {Environment.OSVersion.VersionString}");
        Console.WriteLine();

        // Check if agent is running (basic check - could be improved)
        var agentProcesses = System.Diagnostics.Process.GetProcessesByName("signalbeam-agent");
        var isRunning = agentProcesses.Any(p => p.Id != Environment.ProcessId);

        if (isRunning)
        {
            Console.WriteLine("Agent: ✅ Running");
        }
        else
        {
            Console.WriteLine("Agent: ⏸️  Not Running");
            Console.WriteLine();
            Console.WriteLine("Run 'signalbeam-agent run' to start the agent.");
        }

        return 0;
    }
}
