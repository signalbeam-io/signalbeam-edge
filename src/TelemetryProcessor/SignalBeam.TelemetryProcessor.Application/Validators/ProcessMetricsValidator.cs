using FluentValidation;
using SignalBeam.TelemetryProcessor.Application.Commands;

namespace SignalBeam.TelemetryProcessor.Application.Validators;

/// <summary>
/// Validator for ProcessMetricsCommand.
/// </summary>
public class ProcessMetricsValidator : AbstractValidator<ProcessMetricsCommand>
{
    public ProcessMetricsValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.");

        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .WithMessage("Timestamp is required.")
            .LessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("Timestamp cannot be more than 5 minutes in the future.");

        RuleFor(x => x.CpuUsage)
            .GreaterThanOrEqualTo(0)
            .WithMessage("CPU usage must be greater than or equal to 0.")
            .LessThanOrEqualTo(100)
            .WithMessage("CPU usage must be less than or equal to 100.");

        RuleFor(x => x.MemoryUsage)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Memory usage must be greater than or equal to 0.")
            .LessThanOrEqualTo(100)
            .WithMessage("Memory usage must be less than or equal to 100.");

        RuleFor(x => x.DiskUsage)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Disk usage must be greater than or equal to 0.")
            .LessThanOrEqualTo(100)
            .WithMessage("Disk usage must be less than or equal to 100.");

        RuleFor(x => x.UptimeSeconds)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Uptime seconds must be greater than or equal to 0.");

        RuleFor(x => x.RunningContainers)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Running containers must be greater than or equal to 0.");

        RuleFor(x => x.AdditionalMetrics)
            .MaximumLength(8000)
            .WithMessage("Additional metrics cannot exceed 8000 characters.")
            .When(x => x.AdditionalMetrics is not null);
    }
}
