using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.Domain.Queries.TagQuery;
using FluentValidation;

namespace SignalBeam.DeviceManager.Application.Validators;

/// <summary>
/// Validator for UpdateDeviceGroupCommand.
/// </summary>
public class UpdateDeviceGroupValidator : AbstractValidator<UpdateDeviceGroupCommand>
{
    public UpdateDeviceGroupValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.DeviceGroupId)
            .NotEmpty()
            .WithMessage("DeviceGroupId is required.");

        RuleFor(x => x.Name)
            .MaximumLength(100)
            .WithMessage("Group name cannot exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Name));

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Group description cannot exceed 500 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.TagQuery)
            .MaximumLength(1000)
            .WithMessage("Tag query cannot exceed 1000 characters.")
            .Must(BeValidTagQuery)
            .WithMessage("Tag query syntax is invalid.")
            .When(x => !string.IsNullOrWhiteSpace(x.TagQuery));
    }

    private bool BeValidTagQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true; // Null/empty is valid (handled by business logic)
        }

        try
        {
            TagQueryParser.Parse(query);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
