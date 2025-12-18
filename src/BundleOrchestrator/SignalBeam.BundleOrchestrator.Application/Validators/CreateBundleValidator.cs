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
    }
}
