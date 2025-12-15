using SignalBeam.DeviceManager.Application.Commands;
using FluentValidation;

namespace SignalBeam.DeviceManager.Application.Validators;

/// <summary>
/// Validator for UpdateDeviceCommand.
/// </summary>
public class UpdateDeviceValidator : AbstractValidator<UpdateDeviceCommand>
{
    public UpdateDeviceValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.");

        RuleFor(x => x.Name)
            .MaximumLength(200)
            .WithMessage("Device name cannot exceed 200 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Name));

        RuleFor(x => x.Metadata)
            .MaximumLength(4000)
            .WithMessage("Metadata cannot exceed 4000 characters.")
            .When(x => x.Metadata is not null);

        // At least one field must be provided
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Name) || x.Metadata is not null)
            .WithMessage("At least one field (Name or Metadata) must be provided for update.");
    }
}
