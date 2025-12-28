using Microsoft.AspNetCore.Mvc;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.DeviceManager.Application.Services;

namespace SignalBeam.DeviceManager.Host.Endpoints;

/// <summary>
/// Certificate management API endpoints for mTLS.
/// </summary>
public static class CertificateEndpoints
{
    /// <summary>
    /// Maps all certificate-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapCertificateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/certificates")
            .WithTags("Certificates")
            .WithOpenApi();

        group.MapPost("/{deviceId:guid}/issue", IssueCertificate)
            .WithName("IssueCertificate")
            .WithSummary("Issue a new certificate for an approved device")
            .WithDescription("Generates and issues a new mTLS client certificate for an approved device.");

        group.MapPost("/{serialNumber}/renew", RenewCertificate)
            .WithName("RenewCertificate")
            .WithSummary("Renew an expiring certificate")
            .WithDescription("Renews a certificate that is expiring within 30 days.");

        group.MapDelete("/{serialNumber}", RevokeCertificate)
            .WithName("RevokeCertificate")
            .WithSummary("Revoke a device certificate")
            .WithDescription("Revokes a certificate, preventing further authentication with it.");

        group.MapGet("/device/{deviceId:guid}", GetDeviceCertificates)
            .WithName("GetDeviceCertificates")
            .WithSummary("Get all certificates for a device")
            .WithDescription("Retrieves all certificates (active and revoked) for a specific device.");

        // Public endpoint - no authentication required
        group.MapGet("/ca", GetCaCertificate)
            .WithName("GetCaCertificate")
            .WithSummary("Get the CA certificate")
            .WithDescription("Downloads the CA certificate (public) for client verification.")
            .AllowAnonymous();

        return app;
    }

    /// <summary>
    /// Issues a new certificate for an approved device.
    /// </summary>
    private static async Task<IResult> IssueCertificate(
        Guid deviceId,
        IssueCertificateHandler handler,
        [FromQuery] int validityDays = 90,
        CancellationToken cancellationToken = default)
    {
        var command = new IssueCertificateCommand(deviceId, validityDays);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { error = result.Error!.Code, message = result.Error.Message });
    }

    /// <summary>
    /// Renews an expiring certificate.
    /// </summary>
    private static async Task<IResult> RenewCertificate(
        string serialNumber,
        RenewCertificateHandler handler,
        CancellationToken cancellationToken = default)
    {
        var command = new RenewCertificateCommand(serialNumber);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { error = result.Error!.Code, message = result.Error.Message });
    }

    /// <summary>
    /// Revokes a device certificate.
    /// </summary>
    private static async Task<IResult> RevokeCertificate(
        string serialNumber,
        [FromQuery] string? reason,
        RevokeCertificateHandler handler,
        CancellationToken cancellationToken = default)
    {
        var command = new RevokeCertificateCommand(serialNumber, reason);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { error = result.Error!.Code, message = result.Error.Message });
    }

    /// <summary>
    /// Gets all certificates for a device.
    /// </summary>
    private static async Task<IResult> GetDeviceCertificates(
        Guid deviceId,
        GetDeviceCertificatesHandler handler,
        CancellationToken cancellationToken = default)
    {
        var query = new GetDeviceCertificatesQuery(deviceId);
        var result = await handler.Handle(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { error = result.Error!.Code, message = result.Error.Message });
    }

    /// <summary>
    /// Gets the CA certificate (public endpoint, no authentication required).
    /// </summary>
    private static async Task<IResult> GetCaCertificate(
        ICertificateAuthorityService caService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caCert = await caService.GetCaCertificateAsync(cancellationToken);
            return Results.Text(caCert, "application/x-pem-file");
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to retrieve CA certificate",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
