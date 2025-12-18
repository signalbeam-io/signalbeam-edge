using FluentValidation;
using SignalBeam.TelemetryProcessor.Application.Commands;

namespace SignalBeam.TelemetryProcessor.Application.Validators;

/// <summary>
/// Validator for ProcessHeartbeatCommand.
/// </summary>
public class ProcessHeartbeatValidator : AbstractValidator<ProcessHeartbeatCommand>
{
    public ProcessHeartbeatValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.");

        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .WithMessage("Timestamp is required.")
            .LessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("Timestamp cannot be more than 5 minutes in the future.");

        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required.")
            .MaximumLength(50)
            .WithMessage("Status cannot exceed 50 characters.");

        RuleFor(x => x.IpAddress)
            .MaximumLength(45)
            .WithMessage("IP address cannot exceed 45 characters (IPv6 max length).")
            .When(x => x.IpAddress is not null);

        RuleFor(x => x.AdditionalData)
            .MaximumLength(8000)
            .WithMessage("Additional data cannot exceed 8000 characters.")
            .When(x => x.AdditionalData is not null);
    }
}
