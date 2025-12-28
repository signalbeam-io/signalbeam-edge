using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Host.Services;

namespace SignalBeam.EdgeAgent.Host.Commands;

public static class StatusCommand
{
    public static Command Create()
    {
        var command = new Command("status", "Check device registration status and credentials");

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
            var serviceProvider = HostBuilder.BuildServiceProvider();
            var credentialsStore = serviceProvider.GetRequiredService<IDeviceCredentialsStore>();
            var cloudClient = serviceProvider.GetRequiredService<ICloudClient>();

            // Load credentials
            var credentials = await credentialsStore.LoadCredentialsAsync(CancellationToken.None);

            if (credentials == null)
            {
                Console.WriteLine("❌ Device is not registered.");
                Console.WriteLine("   Run 'signalbeam-agent register' to register this device.");
                return 1;
            }

            Console.WriteLine("📋 Device Registration Status");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine();
            Console.WriteLine($"Device ID:      {credentials.DeviceId}");
            Console.WriteLine($"Tenant ID:      {credentials.TenantId}");
            Console.WriteLine($"Status:         {GetStatusEmoji(credentials.RegistrationStatus)} {credentials.RegistrationStatus}");
            Console.WriteLine();

            // Check current status from cloud
            Console.WriteLine("Checking latest status from cloud...");
            try
            {
                var statusResult = await cloudClient.CheckRegistrationStatusAsync(credentials.DeviceId, CancellationToken.None);

                if (statusResult.Status != credentials.RegistrationStatus)
                {
                    Console.WriteLine($"⚠️  Status changed: {credentials.RegistrationStatus} → {statusResult.Status}");

                    // Update local credentials
                    credentials.RegistrationStatus = statusResult.Status;
                    if (!string.IsNullOrEmpty(statusResult.ApiKey))
                    {
                        credentials.ApiKey = statusResult.ApiKey;
                        credentials.ApiKeyExpiresAt = statusResult.ApiKeyExpiresAt;
                    }
                    await credentialsStore.SaveCredentialsAsync(credentials, CancellationToken.None);
                    Console.WriteLine("✅ Local credentials updated.");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Could not reach cloud: {ex.Message}");
                Console.WriteLine("   Showing cached status from local credentials.");
                Console.WriteLine();
            }

            // Display detailed status
            switch (credentials.RegistrationStatus)
            {
                case "Pending":
                    Console.WriteLine("⏳ Registration Status: PENDING");
                    Console.WriteLine();
                    Console.WriteLine("Your device registration is awaiting approval by an administrator.");
                    Console.WriteLine("The agent cannot start until the device is approved.");
                    Console.WriteLine();
                    Console.WriteLine("What to do:");
                    Console.WriteLine("  1. Contact your administrator to approve this device");
                    Console.WriteLine("  2. Run 'signalbeam-agent status' again to check for updates");
                    Console.WriteLine("  3. Once approved, run 'signalbeam-agent run' to start the agent");
                    break;

                case "Approved":
                    Console.WriteLine("✅ Registration Status: APPROVED");
                    Console.WriteLine();

                    if (string.IsNullOrEmpty(credentials.ApiKey))
                    {
                        Console.WriteLine("⚠️  API key not yet received.");
                        Console.WriteLine("   This is unusual. Try running 'signalbeam-agent status' again.");
                    }
                    else
                    {
                        Console.WriteLine($"API Key:        {credentials.ApiKey[..20]}... (truncated for security)");

                        if (credentials.ApiKeyExpiresAt.HasValue)
                        {
                            var daysUntilExpiration = (credentials.ApiKeyExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays;

                            if (daysUntilExpiration < 0)
                            {
                                Console.WriteLine($"Expires:        ❌ EXPIRED ({credentials.ApiKeyExpiresAt:yyyy-MM-dd})");
                                Console.WriteLine();
                                Console.WriteLine("⚠️  Your API key has expired!");
                                Console.WriteLine("   Contact your administrator to rotate the API key.");
                            }
                            else if (daysUntilExpiration < 7)
                            {
                                Console.WriteLine($"Expires:        ⚠️  {credentials.ApiKeyExpiresAt:yyyy-MM-dd} ({daysUntilExpiration:F1} days)");
                                Console.WriteLine();
                                Console.WriteLine($"⚠️  API key expires in {daysUntilExpiration:F1} days!");
                                Console.WriteLine("   Consider rotating the key soon.");
                            }
                            else
                            {
                                Console.WriteLine($"Expires:        {credentials.ApiKeyExpiresAt:yyyy-MM-dd} ({daysUntilExpiration:F0} days)");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Expires:        Never");
                        }

                        Console.WriteLine();
                        Console.WriteLine("✅ Device is ready!");
                        Console.WriteLine("   Run 'signalbeam-agent run' to start the agent.");
                    }
                    break;

                case "Rejected":
                    Console.WriteLine("❌ Registration Status: REJECTED");
                    Console.WriteLine();
                    Console.WriteLine("Your device registration has been rejected.");
                    Console.WriteLine("Contact your administrator for more information.");
                    Console.WriteLine("You may need to register a new device.");
                    break;

                default:
                    Console.WriteLine($"❓ Registration Status: {credentials.RegistrationStatus} (Unknown)");
                    break;
            }

            Console.WriteLine();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to check status: {ex.Message}");
            return 1;
        }
    }

    private static string GetStatusEmoji(string status)
    {
        return status switch
        {
            "Pending" => "⏳",
            "Approved" => "✅",
            "Rejected" => "❌",
            _ => "❓"
        };
    }
}
