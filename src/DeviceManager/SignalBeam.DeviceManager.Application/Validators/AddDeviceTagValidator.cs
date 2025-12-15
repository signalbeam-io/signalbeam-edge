using SignalBeam.DeviceManager.Application.Commands;
using FluentValidation;

namespace SignalBeam.DeviceManager.Application.Validators;

/// <summary>
/// Validator for AddDeviceTagCommand.
/// </summary>
public class AddDeviceTagValidator : AbstractValidator<AddDeviceTagCommand>
{
    public AddDeviceTagValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.");

        RuleFor(x => x.Tag)
            .NotEmpty()
            .WithMessage("Tag is required.")
            .MaximumLength(50)
            .WithMessage("Tag cannot exceed 50 characters.")
            .Matches(@"^[a-z0-9-]+$")
            .WithMessage("Tag must contain only lowercase letters, numbers, and hyphens.");
    }
}
