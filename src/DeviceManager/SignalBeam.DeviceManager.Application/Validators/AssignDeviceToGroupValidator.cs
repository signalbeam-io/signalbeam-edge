using SignalBeam.DeviceManager.Application.Commands;
using FluentValidation;

namespace SignalBeam.DeviceManager.Application.Validators;

public class AssignDeviceToGroupValidator : AbstractValidator<AssignDeviceToGroupCommand>
{
    public AssignDeviceToGroupValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.");

        // DeviceGroupId can be null (to unassign from group)
    }
}
