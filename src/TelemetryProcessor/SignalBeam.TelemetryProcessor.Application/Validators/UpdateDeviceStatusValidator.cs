using FluentValidation;
using SignalBeam.TelemetryProcessor.Application.Commands;

namespace SignalBeam.TelemetryProcessor.Application.Validators;

/// <summary>
/// Validator for UpdateDeviceStatusCommand.
/// </summary>
public class UpdateDeviceStatusValidator : AbstractValidator<UpdateDeviceStatusCommand>
{
    public UpdateDeviceStatusValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.");

        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("Invalid device status.");

        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .WithMessage("Timestamp is required.")
            .LessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("Timestamp cannot be more than 5 minutes in the future.");
    }
}
