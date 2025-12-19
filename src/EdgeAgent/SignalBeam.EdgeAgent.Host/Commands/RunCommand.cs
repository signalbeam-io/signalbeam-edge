using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Host.Services;

namespace SignalBeam.EdgeAgent.Host.Commands;

public static class RunCommand
{
    public static Command Create()
    {
        var command = new Command("run", "Run the SignalBeam agent (heartbeat + reconciliation loops)");

        command.SetHandler(async () =>
        {
            await ExecuteAsync();
        });

        return command;
    }

    private static async Task<int> ExecuteAsync()
    {
        try
        {
            var host = HostBuilder.BuildHost();

            // Check if device is registered
            var stateManager = host.Services.GetRequiredService<DeviceStateManager>();
            if (!stateManager.IsRegistered)
            {
                Console.WriteLine("❌ Device is not registered. Please run 'signalbeam-agent register' first.");
                return 1;
            }

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Starting SignalBeam Edge Agent");
            logger.LogInformation("Device ID: {DeviceId}", stateManager.DeviceId);

            Console.WriteLine("🚀 SignalBeam Edge Agent starting...");
            Console.WriteLine($"   Device ID: {stateManager.DeviceId}");
            Console.WriteLine($"   Cloud Endpoint: {stateManager.CloudEndpoint}");
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to stop the agent");
            Console.WriteLine();

            await host.RunAsync();

            logger.LogInformation("SignalBeam Edge Agent stopped");
            Console.WriteLine("👋 SignalBeam Edge Agent stopped");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to start agent: {ex.Message}");
            return 1;
        }
    }
}
