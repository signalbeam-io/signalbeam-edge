using FluentValidation;
using SignalBeam.BundleOrchestrator.Application.Commands;

namespace SignalBeam.BundleOrchestrator.Application.Validators;

/// <summary>
/// Validator for CreatePhasedRolloutCommand.
/// </summary>
public class CreatePhasedRolloutValidator : AbstractValidator<CreatePhasedRolloutCommand>
{
    public CreatePhasedRolloutValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.BundleId)
            .NotEmpty()
            .WithMessage("BundleId is required.")
            .Must(BeValidBundleId)
            .WithMessage("BundleId must be in format 'org/name' or 'name'.");

        RuleFor(x => x.TargetVersion)
            .NotEmpty()
            .WithMessage("TargetVersion is required.")
            .Must(BeValidSemVer)
            .WithMessage("TargetVersion must be a valid semantic version (e.g., '1.0.0').");

        RuleFor(x => x.PreviousVersion)
            .Must(BeValidSemVerWhenProvided)
            .When(x => !string.IsNullOrWhiteSpace(x.PreviousVersion))
            .WithMessage("PreviousVersion must be a valid semantic version when provided.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Rollout name is required.")
            .MaximumLength(200)
            .WithMessage("Rollout name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.Description))
            .WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.Phases)
            .NotEmpty()
            .WithMessage("At least one phase is required.")
            .Must(phases => phases.Count >= 1 && phases.Count <= 10)
            .WithMessage("Rollout must have between 1 and 10 phases.");

        RuleForEach(x => x.Phases)
            .SetValidator(new PhaseConfigValidator());

        RuleFor(x => x.FailureThreshold)
            .InclusiveBetween(0.0m, 1.0m)
            .WithMessage("FailureThreshold must be between 0.0 and 1.0 (0% to 100%).");

        RuleFor(x => x.CreatedBy)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.CreatedBy))
            .WithMessage("CreatedBy must not exceed 200 characters.");
    }

    private bool BeValidBundleId(string bundleId)
    {
        if (string.IsNullOrWhiteSpace(bundleId))
            return false;

        // BundleId format: "org/name" or "name"
        var parts = bundleId.Split('/');
        return parts.Length is 1 or 2 &&
               parts.All(p => !string.IsNullOrWhiteSpace(p) && p.Length <= 100);
    }

    private bool BeValidSemVer(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        // Simple semantic version validation (major.minor.patch)
        var parts = version.Split('.');
        return parts.Length == 3 &&
               parts.All(p => int.TryParse(p, out _));
    }

    private bool BeValidSemVerWhenProvided(string? version)
    {
        return string.IsNullOrWhiteSpace(version) || BeValidSemVer(version);
    }
}

/// <summary>
/// Validator for PhaseConfigDto.
/// </summary>
public class PhaseConfigValidator : AbstractValidator<PhaseConfigDto>
{
    public PhaseConfigValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Phase name is required.")
            .MaximumLength(100)
            .WithMessage("Phase name must not exceed 100 characters.");

        RuleFor(x => x.TargetPercentage)
            .GreaterThan(0)
            .WithMessage("TargetPercentage must be greater than 0.")
            .LessThanOrEqualTo(100)
            .WithMessage("TargetPercentage must not exceed 100.");

        RuleFor(x => x.MinHealthyDurationMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinHealthyDurationMinutes.HasValue)
            .WithMessage("MinHealthyDurationMinutes must be non-negative.")
            .LessThanOrEqualTo(1440) // 24 hours
            .When(x => x.MinHealthyDurationMinutes.HasValue)
            .WithMessage("MinHealthyDurationMinutes must not exceed 1440 (24 hours).");
    }
}
