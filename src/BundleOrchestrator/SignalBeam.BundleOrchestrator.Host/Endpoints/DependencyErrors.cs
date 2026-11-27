using SignalBeam.BundleOrchestrator.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Host.Endpoints;

// A dependency (DeviceManager) couldn't be reached while reading a target group (#491). That's
// retryable, so it's a 503; other Failure errors keep their own mapping.
public static class DependencyErrors
{
    public static bool IsUnavailable(Error error) => error.Code == DeviceGroupErrors.UnavailableCode;

    public static IResult ServiceUnavailable(Error error) => Results.Json(
        new { error = error.Code, message = error.Message, type = error.Type.ToString() },
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
