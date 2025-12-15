using SignalBeam.DeviceManager.Application.Commands;
using FluentValidation;

namespace SignalBeam.DeviceManager.Application.Validators;

public class UpdateDeviceMetricsValidator : AbstractValidator<UpdateDeviceMetricsCommand>
{
    public UpdateDeviceMetricsValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.");

        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .WithMessage("Timestamp is required.")
            .LessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("Timestamp cannot be in the future (allowing 5 minutes clock skew).");

        RuleFor(x => x.CpuUsage)
            .InclusiveBetween(0, 100)
            .WithMessage("CPU usage must be between 0 and 100.");

        RuleFor(x => x.MemoryUsage)
            .InclusiveBetween(0, 100)
            .WithMessage("Memory usage must be between 0 and 100.");

        RuleFor(x => x.DiskUsage)
            .InclusiveBetween(0, 100)
            .WithMessage("Disk usage must be between 0 and 100.");

        RuleFor(x => x.UptimeSeconds)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Uptime seconds must be non-negative.");

        RuleFor(x => x.RunningContainers)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Running containers must be non-negative.");
    }
}
