using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Host.Configuration;
using SignalBeam.EdgeAgent.Host.Services;
using Wolverine;

namespace SignalBeam.EdgeAgent.Host.Commands;

public static class RegisterCommand
{
    public static Command Create()
    {
        var command = new Command("register", "Register this device with the SignalBeam cloud");

        var tenantIdOption = new Option<Guid>("--tenant-id")
        {
            Description = "The tenant ID for this device",
            Required = true
        };

        var deviceIdOption = new Option<string?>("--device-id")
        {
            Description = "The unique device ID (auto-generated if not provided)"
        };

        var tokenOption = new Option<string>("--token")
        {
            Description = "The registration token",
            Required = true
        };

        var cloudUrlOption = new Option<string>("--cloud-url")
        {
            Description = "The SignalBeam cloud API URL",
            DefaultValueFactory = _ => "https://api.signalbeam.com"
        };

        command.Options.Add(tenantIdOption);
        command.Options.Add(deviceIdOption);
        command.Options.Add(tokenOption);
        command.Options.Add(cloudUrlOption);

        // Required options are validated by the parser, so GetValue never returns null for them.
        command.SetAction((parseResult, _) => ExecuteAsync(
            parseResult.GetValue(tenantIdOption),
            parseResult.GetValue(deviceIdOption),
            parseResult.GetValue(tokenOption)!,
            parseResult.GetValue(cloudUrlOption)!));

        return command;
    }

    private static async Task<int> ExecuteAsync(Guid tenantId, string? deviceId, string token, string cloudUrl)
    {
        // Point the cloud client at the requested control plane before the provider is built.
        Environment.SetEnvironmentVariable("Agent__CloudUrl", cloudUrl);

        // Persist the URL so `run` (e.g. the systemd service) talks to the same control plane.
        // Non-fatal: in containers the URL usually comes from Agent__CloudUrl instead.
        var configFilePath = AgentPaths.ConfigFilePath;
        try
        {
            AgentConfigFile.SetCloudUrl(configFilePath, cloudUrl);
            Console.WriteLine($"📝 Saved control-plane URL to {configFilePath}");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Console.WriteLine($"⚠️  Could not save the control-plane URL to {configFilePath}: {ex.Message}");
            Console.WriteLine("   Run register as the 'signalbeam' user (sudo -u signalbeam ...) or set");
            Console.WriteLine($"   Agent__CloudUrl={cloudUrl} for the service, otherwise 'run' uses the default URL.");
        }

        var serviceProvider = HostBuilder.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<Program>();

        try
        {
            // Auto-generate device ID if not provided
            var effectiveDeviceId = deviceId ?? Guid.NewGuid().ToString();

            logger.LogInformation("Registering device {DeviceId} with tenant {TenantId}", effectiveDeviceId, tenantId);

            var hostname = Environment.MachineName;
            var platform = Environment.OSVersion.Platform.ToString();

            var command = new RegisterDeviceCommand(
                tenantId,
                effectiveDeviceId,
                token,
                hostname,
                platform);

            var handler = serviceProvider.GetRequiredService<RegisterDeviceCommandHandler>();
            var result = await handler.Handle(command, CancellationToken.None);

            if (!result.IsSuccess || result.Value == null)
            {
                logger.LogError("Registration failed: {Error}", result.Error?.Message ?? "Unknown error");
                Console.WriteLine($"❌ Registration failed: {result.Error?.Message ?? "Unknown error"}");
                return 1;
            }

            var response = result.Value;

            logger.LogInformation("Device registered successfully with ID: {DeviceId}, Status: {Status}", response.DeviceId, response.Status);

            Console.WriteLine("✅ Device registered successfully!");
            Console.WriteLine($"   Device ID: {response.DeviceId}");
            Console.WriteLine($"   Device Name: {response.Name}");
            Console.WriteLine($"   Registration Status: {response.Status}");
            Console.WriteLine();

            if (response.Status == "Pending")
            {
                Console.WriteLine("⏳ Registration is pending approval by an administrator.");
                Console.WriteLine("   Once approved, the agent will automatically receive its API key.");
                Console.WriteLine("   You can check status with: signalbeam-agent status");
            }
            else if (response.Status == "Approved" && response.ApiKey != null)
            {
                Console.WriteLine("✅ Registration approved! API key has been saved.");
                Console.WriteLine("   You can now run the agent with: signalbeam-agent run");
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Registration failed");
            Console.WriteLine($"❌ Registration failed: {ex.Message}");
            return 1;
        }
    }
}
