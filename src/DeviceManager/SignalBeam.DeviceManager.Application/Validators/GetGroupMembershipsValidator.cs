using SignalBeam.DeviceManager.Application.Queries;
using FluentValidation;

namespace SignalBeam.DeviceManager.Application.Validators;

/// <summary>
/// Validator for GetGroupMembershipsQuery.
/// </summary>
public class GetGroupMembershipsValidator : AbstractValidator<GetGroupMembershipsQuery>
{
    public GetGroupMembershipsValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.DeviceGroupId)
            .NotEmpty()
            .WithMessage("DeviceGroupId is required.");
    }
}
