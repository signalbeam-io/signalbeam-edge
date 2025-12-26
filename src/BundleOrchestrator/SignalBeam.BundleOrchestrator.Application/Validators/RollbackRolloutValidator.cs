using FluentValidation;
using SignalBeam.BundleOrchestrator.Application.Commands;

namespace SignalBeam.BundleOrchestrator.Application.Validators;

/// <summary>
/// Validator for RollbackRolloutCommand.
/// </summary>
public class RollbackRolloutValidator : AbstractValidator<RollbackRolloutCommand>
{
    public RollbackRolloutValidator()
    {
        RuleFor(x => x.RolloutId)
            .NotEmpty()
            .WithMessage("RolloutId is required.");
    }
}
