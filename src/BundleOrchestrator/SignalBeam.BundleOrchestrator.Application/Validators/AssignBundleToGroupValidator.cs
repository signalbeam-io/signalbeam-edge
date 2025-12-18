using SignalBeam.BundleOrchestrator.Application.Commands;
using FluentValidation;

namespace SignalBeam.BundleOrchestrator.Application.Validators;

/// <summary>
/// Validator for AssignBundleToGroupCommand.
/// </summary>
public class AssignBundleToGroupValidator : AbstractValidator<AssignBundleToGroupCommand>
{
    public AssignBundleToGroupValidator()
    {
        RuleFor(x => x.DeviceGroupId)
            .NotEmpty()
            .WithMessage("DeviceGroupId is required.")
            .Must(BeValidGuid)
            .WithMessage("DeviceGroupId must be a valid GUID.");

        RuleFor(x => x.BundleId)
            .NotEmpty()
            .WithMessage("BundleId is required.")
            .Must(BeValidGuid)
            .WithMessage("BundleId must be a valid GUID.");

        RuleFor(x => x.Version)
            .NotEmpty()
            .WithMessage("Version is required.")
            .Matches(@"^\d+\.\d+\.\d+(?:-[\w\.-]+)?$")
            .WithMessage("Version must be a valid semantic version (e.g., 1.0.0 or 1.0.0-beta.1).");

        RuleFor(x => x.AssignedBy)
            .MaximumLength(200)
            .WithMessage("AssignedBy cannot exceed 200 characters.")
            .When(x => x.AssignedBy is not null);
    }

    private bool BeValidGuid(string value)
    {
        return Guid.TryParse(value, out _);
    }
}
