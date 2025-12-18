using SignalBeam.BundleOrchestrator.Application.Commands;
using FluentValidation;

namespace SignalBeam.BundleOrchestrator.Application.Validators;

/// <summary>
/// Validator for UpdateRolloutStatusCommand.
/// </summary>
public class UpdateRolloutStatusValidator : AbstractValidator<UpdateRolloutStatusCommand>
{
    public UpdateRolloutStatusValidator()
    {
        RuleFor(x => x.RolloutId)
            .NotEmpty()
            .WithMessage("RolloutId is required.");

        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required.")
            .Must(BeValidStatus)
            .WithMessage("Status must be one of: Pending, InProgress, Succeeded, Failed.");

        RuleFor(x => x.ErrorMessage)
            .NotEmpty()
            .WithMessage("ErrorMessage is required when status is Failed.")
            .When(x => x.Status?.Equals("Failed", StringComparison.OrdinalIgnoreCase) == true);

        RuleFor(x => x.ErrorMessage)
            .MaximumLength(2000)
            .WithMessage("ErrorMessage cannot exceed 2000 characters.")
            .When(x => x.ErrorMessage is not null);
    }

    private bool BeValidStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        var validStatuses = new[] { "Pending", "InProgress", "Succeeded", "Failed" };
        return validStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);
    }
}
