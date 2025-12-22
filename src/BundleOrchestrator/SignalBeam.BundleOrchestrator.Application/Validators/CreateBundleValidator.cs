using SignalBeam.BundleOrchestrator.Application.Commands;
using FluentValidation;

namespace SignalBeam.BundleOrchestrator.Application.Validators;

/// <summary>
/// Validator for CreateBundleCommand.
/// </summary>
public class CreateBundleValidator : AbstractValidator<CreateBundleCommand>
{
    public CreateBundleValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Bundle name is required.")
            .MaximumLength(200)
            .WithMessage("Bundle name cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description cannot exceed 1000 characters.")
            .When(x => x.Description is not null);

        // Version and containers must both be provided or both be omitted
        RuleFor(x => x)
            .Must(x =>
            {
                var hasVersion = !string.IsNullOrWhiteSpace(x.Version);
                var hasContainers = x.Containers?.Count > 0;
                return hasVersion == hasContainers;
            })
            .WithMessage("Version and containers must both be provided or both be omitted.")
            .WithName("Version/Containers");

        // Validate version format if provided
        RuleFor(x => x.Version)
            .Matches(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?$")
            .WithMessage("Version must be a valid semantic version (e.g., 1.0.0 or 1.0.0-beta)")
            .When(x => !string.IsNullOrWhiteSpace(x.Version));

        // Validate containers if provided
        RuleFor(x => x.Containers)
            .NotEmpty()
            .WithMessage("At least one container is required when creating a version.")
            .When(x => x.Containers is not null);

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
            })
            .When(x => x.Containers is not null);
    }
}
