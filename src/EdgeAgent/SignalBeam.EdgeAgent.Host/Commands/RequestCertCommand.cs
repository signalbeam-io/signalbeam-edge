using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Commands;

namespace SignalBeam.EdgeAgent.Host.Commands;

/// <summary>
/// Provisions an mTLS client certificate for this device: generates a key pair and CSR locally,
/// has the cloud CA sign it, and stores the certificate. The private key never leaves the device.
/// </summary>
public static class RequestCertCommand
{
    public static Command Create()
    {
        var command = new Command("request-cert", "Provision an mTLS client certificate from the cloud CA");

        command.SetHandler(async () => await ExecuteAsync());

        return command;
    }

    private static async Task<int> ExecuteAsync()
    {
        var serviceProvider = HostBuilder.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();

        try
        {
            var handler = serviceProvider.GetRequiredService<RequestCertificateCommandHandler>();
            var result = await handler.Handle(new RequestCertificateCommand(), CancellationToken.None);

            if (!result.IsSuccess || result.Value is null)
            {
                Console.WriteLine($"❌ Certificate provisioning failed: {result.Error?.Message ?? "Unknown error"}");
                return 1;
            }

            Console.WriteLine("✅ mTLS certificate provisioned successfully!");
            Console.WriteLine($"   Serial: {result.Value.SerialNumber}");
            Console.WriteLine($"   Expires: {result.Value.ExpiresAt:u}");
            Console.WriteLine($"   Certificate: {result.Value.CertificatePath}");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Certificate provisioning failed");
            Console.WriteLine($"❌ Certificate provisioning failed: {ex.Message}");
            return 1;
        }
    }
}
