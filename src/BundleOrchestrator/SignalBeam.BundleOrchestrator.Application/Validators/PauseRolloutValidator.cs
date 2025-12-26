using FluentValidation;
using SignalBeam.BundleOrchestrator.Application.Commands;

namespace SignalBeam.BundleOrchestrator.Application.Validators;

/// <summary>
/// Validator for PauseRolloutCommand.
/// </summary>
public class PauseRolloutValidator : AbstractValidator<PauseRolloutCommand>
{
    public PauseRolloutValidator()
    {
        RuleFor(x => x.RolloutId)
            .NotEmpty()
            .WithMessage("RolloutId is required.");
    }
}
