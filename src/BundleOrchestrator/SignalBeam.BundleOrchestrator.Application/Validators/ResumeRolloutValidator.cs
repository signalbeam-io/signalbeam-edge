using FluentValidation;
using SignalBeam.BundleOrchestrator.Application.Commands;

namespace SignalBeam.BundleOrchestrator.Application.Validators;

/// <summary>
/// Validator for ResumeRolloutCommand.
/// </summary>
public class ResumeRolloutValidator : AbstractValidator<ResumeRolloutCommand>
{
    public ResumeRolloutValidator()
    {
        RuleFor(x => x.RolloutId)
            .NotEmpty()
            .WithMessage("RolloutId is required.");
    }
}
