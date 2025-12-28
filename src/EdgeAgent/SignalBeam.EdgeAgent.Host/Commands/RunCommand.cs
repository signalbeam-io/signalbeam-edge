using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;
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

            // Check credentials and registration status
            var credentialsStore = host.Services.GetRequiredService<IDeviceCredentialsStore>();
            var credentials = await credentialsStore.LoadCredentialsAsync(CancellationToken.None);

            if (credentials == null)
            {
                Console.WriteLine("❌ Device credentials not found. Please run 'signalbeam-agent register' first.");
                return 1;
            }

            // Check registration status
            if (credentials.RegistrationStatus == "Pending")
            {
                Console.WriteLine("⏳ Device registration is pending approval.");
                Console.WriteLine("   The agent cannot start until an administrator approves the device.");
                Console.WriteLine("   Check status with: signalbeam-agent status");
                return 1;
            }

            if (credentials.RegistrationStatus == "Rejected")
            {
                Console.WriteLine("❌ Device registration has been rejected.");
                Console.WriteLine("   Please contact your administrator or register a new device.");
                return 1;
            }

            // Check API key
            if (string.IsNullOrEmpty(credentials.ApiKey))
            {
                Console.WriteLine("❌ Device API key not found.");
                Console.WriteLine("   Registration may still be pending approval.");
                Console.WriteLine("   Check status with: signalbeam-agent status");
                return 1;
            }

            // Check API key expiration
            if (credentials.ApiKeyExpiresAt.HasValue)
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
                    Console.WriteLine("   Consider rotating the key soon.");
                    Console.WriteLine();
                }
            }

            Console.WriteLine("🚀 SignalBeam Edge Agent starting...");
            Console.WriteLine($"   Device ID: {stateManager.DeviceId}");
            Console.WriteLine($"   Cloud Endpoint: {stateManager.CloudEndpoint}");
            Console.WriteLine($"   Registration Status: {credentials.RegistrationStatus}");
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
