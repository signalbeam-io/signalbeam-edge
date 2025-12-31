using SignalBeam.DeviceManager.Application.Commands;
using FluentValidation;

namespace SignalBeam.DeviceManager.Application.Validators;

/// <summary>
/// Validator for RemoveDeviceTagCommand.
/// </summary>
public class RemoveDeviceTagValidator : AbstractValidator<RemoveDeviceTagCommand>
{
    public RemoveDeviceTagValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.");

        RuleFor(x => x.Tag)
            .NotEmpty()
            .WithMessage("Tag is required.")
            .MaximumLength(100)
            .WithMessage("Tag cannot exceed 100 characters.");
    }
}
