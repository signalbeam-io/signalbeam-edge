using SignalBeam.DeviceManager.Application.Commands;
using FluentValidation;

namespace SignalBeam.DeviceManager.Application.Validators;

/// <summary>
/// Validator for RegisterDeviceCommand.
/// </summary>
public class RegisterDeviceValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Device name is required.")
            .MaximumLength(200)
            .WithMessage("Device name cannot exceed 200 characters.");

        RuleFor(x => x.Metadata)
            .MaximumLength(4000)
            .WithMessage("Metadata cannot exceed 4000 characters.")
            .When(x => x.Metadata is not null);
    }
}
