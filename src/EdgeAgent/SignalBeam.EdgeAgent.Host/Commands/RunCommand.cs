using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Host.Configuration;
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

            var stateManager = host.Services.GetRequiredService<DeviceStateManager>();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            // Credentials are the source of truth for registration state.
            var credentialsStore = host.Services.GetRequiredService<IDeviceCredentialsStore>();
            var credentials = await credentialsStore.LoadCredentialsAsync(CancellationToken.None);

            if (credentials == null)
            {
                Console.WriteLine("❌ Device credentials not found. Please run 'signalbeam-agent register' first.");
                return 1;
            }

            if (credentials.RegistrationStatus == "Rejected")
            {
                Console.WriteLine("❌ Device registration has been rejected.");
                Console.WriteLine("   Please contact your administrator or register a new device.");
                return 1;
            }

            var hasApiKey = !string.IsNullOrEmpty(credentials.ApiKey);

            // A claimed key that has already expired can't be used and can't be rotated.
            if (hasApiKey && credentials.ApiKeyExpiresAt.HasValue)
            {
                var daysUntilExpiration = (credentials.ApiKeyExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays;

                if (daysUntilExpiration < 0)
                {
                    Console.WriteLine("❌ Device API key has expired.");
                    Console.WriteLine("   Please contact your administrator to rotate the API key.");
                    return 1;
                }

                if (daysUntilExpiration < 7)
                {
                    Console.WriteLine($"⚠️  Warning: API key expires in {daysUntilExpiration:F1} days.");
                    Console.WriteLine("   The agent will attempt to rotate it automatically.");
                    Console.WriteLine();
                }
            }

            logger.LogInformation("Starting SignalBeam Edge Agent");
            logger.LogInformation("Device ID: {DeviceId}", credentials.DeviceId);

            // Promote to registered so the heartbeat & reconciliation loops start. When the key
            // hasn't been claimed yet, RegistrationPollingService does this once approval lands.
            if (hasApiKey)
            {
                var options = host.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
                stateManager.SetRegistrationState(credentials.DeviceId, credentials.ApiKey!, options.CloudUrl);
            }

            Console.WriteLine("🚀 SignalBeam Edge Agent starting...");
            Console.WriteLine($"   Device ID: {credentials.DeviceId}");
            Console.WriteLine($"   Registration Status: {credentials.RegistrationStatus}");

            if (!hasApiKey)
            {
                Console.WriteLine();
                Console.WriteLine("⏳ Registration is pending approval. The agent will poll and start the");
                Console.WriteLine("   heartbeat automatically once approved and its API key is claimed.");
            }

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
