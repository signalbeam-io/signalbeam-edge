using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Host.Services;
using Wolverine;

namespace SignalBeam.EdgeAgent.Host.Commands;

public static class RegisterCommand
{
    public static Command Create()
    {
        var command = new Command("register", "Register this device with the SignalBeam cloud");

        var tenantIdOption = new Option<Guid>(
            name: "--tenant-id",
            description: "The tenant ID for this device")
        {
            IsRequired = true
        };

        var deviceIdOption = new Option<string?>(
            name: "--device-id",
            description: "The unique device ID (auto-generated if not provided)")
        {
            IsRequired = false
        };

        var tokenOption = new Option<string>(
            name: "--token",
            description: "The registration token")
        {
            IsRequired = true
        };

        var cloudUrlOption = new Option<string>(
            name: "--cloud-url",
            description: "The SignalBeam cloud API URL",
            getDefaultValue: () => "https://api.signalbeam.com");

        command.AddOption(tenantIdOption);
        command.AddOption(deviceIdOption);
        command.AddOption(tokenOption);
        command.AddOption(cloudUrlOption);

        command.SetHandler(async (tenantId, deviceId, token, cloudUrl) =>
        {
            await ExecuteAsync(tenantId, deviceId, token, cloudUrl);
        }, tenantIdOption, deviceIdOption, tokenOption, cloudUrlOption);

        return command;
    }

    private static async Task<int> ExecuteAsync(Guid tenantId, string? deviceId, string token, string cloudUrl)
    {
        var serviceProvider = HostBuilder.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<Program>();
        var cloudClient = serviceProvider.GetRequiredService<ICloudClient>();
        var credentialsStore = serviceProvider.GetRequiredService<IDeviceCredentialsStore>();

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

            var handlerLogger = loggerFactory.CreateLogger<RegisterDeviceCommandHandler>();
            var handler = new RegisterDeviceCommandHandler(cloudClient, credentialsStore, handlerLogger);
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
