using SignalBeam.DeviceManager.Application.Queries;
using FluentValidation;

namespace SignalBeam.DeviceManager.Application.Validators;

/// <summary>
/// Validator for GetAllTagsQuery.
/// </summary>
public class GetAllTagsValidator : AbstractValidator<GetAllTagsQuery>
{
    public GetAllTagsValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");
    }
}
