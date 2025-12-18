using FluentValidation;
using SignalBeam.TelemetryProcessor.Application.Queries;

namespace SignalBeam.TelemetryProcessor.Application.Validators;

/// <summary>
/// Validator for GetAggregatedMetricsQuery.
/// </summary>
public class GetAggregatedMetricsValidator : AbstractValidator<GetAggregatedMetricsQuery>
{
    public GetAggregatedMetricsValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.");

        RuleFor(x => x.StartTime)
            .NotEmpty()
            .WithMessage("Start time is required.")
            .LessThan(x => x.EndTime)
            .WithMessage("Start time must be before end time.");

        RuleFor(x => x.EndTime)
            .NotEmpty()
            .WithMessage("End time is required.");

        RuleFor(x => x.Interval)
            .IsInEnum()
            .WithMessage("Invalid aggregation interval.");
    }
}
