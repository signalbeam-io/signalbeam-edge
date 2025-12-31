using SignalBeam.DeviceManager.Application.Commands;
using FluentValidation;

namespace SignalBeam.DeviceManager.Application.Validators;

/// <summary>
/// Validator for AddDeviceToGroupCommand.
/// </summary>
public class AddDeviceToGroupValidator : AbstractValidator<AddDeviceToGroupCommand>
{
    public AddDeviceToGroupValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.DeviceGroupId)
            .NotEmpty()
            .WithMessage("DeviceGroupId is required.");

        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.");
    }
}
