using FluentValidation;
using SignalBeam.TelemetryProcessor.Application.Queries;

namespace SignalBeam.TelemetryProcessor.Application.Validators;

/// <summary>
/// Validator for GetDeviceMetricsQuery.
/// </summary>
public class GetDeviceMetricsValidator : AbstractValidator<GetDeviceMetricsQuery>
{
    public GetDeviceMetricsValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0.");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("Page size must be greater than 0.")
            .LessThanOrEqualTo(1000)
            .WithMessage("Page size cannot exceed 1000.");

        RuleFor(x => x)
            .Must(x => !x.StartTime.HasValue || !x.EndTime.HasValue || x.StartTime.Value < x.EndTime.Value)
            .WithMessage("Start time must be before end time.")
            .When(x => x.StartTime.HasValue && x.EndTime.HasValue);
    }
}
