using FluentValidation;
using SignalBeam.BundleOrchestrator.Application.Commands;

namespace SignalBeam.BundleOrchestrator.Application.Validators;

/// <summary>
/// Validator for AdvancePhaseCommand.
/// </summary>
public class AdvancePhaseValidator : AbstractValidator<AdvancePhaseCommand>
{
    public AdvancePhaseValidator()
    {
        RuleFor(x => x.RolloutId)
            .NotEmpty()
            .WithMessage("RolloutId is required.");
    }
}
