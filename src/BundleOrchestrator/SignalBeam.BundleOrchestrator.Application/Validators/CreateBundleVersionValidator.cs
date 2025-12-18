using SignalBeam.BundleOrchestrator.Application.Commands;
using FluentValidation;

namespace SignalBeam.BundleOrchestrator.Application.Validators;

/// <summary>
/// Validator for CreateBundleVersionCommand.
/// </summary>
public class CreateBundleVersionValidator : AbstractValidator<CreateBundleVersionCommand>
{
    public CreateBundleVersionValidator()
    {
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

        RuleFor(x => x.Containers)
            .NotEmpty()
            .WithMessage("At least one container is required.")
            .Must(containers => containers != null && containers.Count > 0)
            .WithMessage("Bundle version must have at least one container.");

        RuleForEach(x => x.Containers)
            .ChildRules(container =>
            {
                container.RuleFor(c => c.Name)
                    .NotEmpty()
                    .WithMessage("Container name is required.")
                    .MaximumLength(100)
                    .WithMessage("Container name cannot exceed 100 characters.");

                container.RuleFor(c => c.Image)
                    .NotEmpty()
                    .WithMessage("Container image is required.")
                    .MaximumLength(500)
                    .WithMessage("Container image cannot exceed 500 characters.");
            });

        RuleFor(x => x.ReleaseNotes)
            .MaximumLength(5000)
            .WithMessage("Release notes cannot exceed 5000 characters.")
            .When(x => x.ReleaseNotes is not null);
    }

    private bool BeValidGuid(string value)
    {
        return Guid.TryParse(value, out _);
    }
}
