using SignalBeam.DeviceManager.Application.Commands;
using FluentValidation;

namespace SignalBeam.DeviceManager.Application.Validators;

/// <summary>
/// Validator for BulkAddDeviceTagsCommand.
/// </summary>
public class BulkAddDeviceTagsValidator : AbstractValidator<BulkAddDeviceTagsCommand>
{
    public BulkAddDeviceTagsValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.DeviceGroupId)
            .NotEmpty()
            .WithMessage("DeviceGroupId is required.");

        RuleFor(x => x.Tag)
            .NotEmpty()
            .WithMessage("Tag is required.")
            .MaximumLength(100)
            .WithMessage("Tag cannot exceed 100 characters.");
    }
}
