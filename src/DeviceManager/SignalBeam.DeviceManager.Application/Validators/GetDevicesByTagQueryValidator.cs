using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.Domain.Queries.TagQuery;
using FluentValidation;

namespace SignalBeam.DeviceManager.Application.Validators;

/// <summary>
/// Validator for GetDevicesByTagQueryQuery.
/// </summary>
public class GetDevicesByTagQueryValidator : AbstractValidator<GetDevicesByTagQueryQuery>
{
    public GetDevicesByTagQueryValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.TagQueryString)
            .NotEmpty()
            .WithMessage("TagQueryString is required.")
            .MaximumLength(1000)
            .WithMessage("Tag query cannot exceed 1000 characters.")
            .Must(BeValidTagQuery)
            .WithMessage("Tag query syntax is invalid.");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("PageNumber must be greater than 0.");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("PageSize must be greater than 0.")
            .LessThanOrEqualTo(100)
            .WithMessage("PageSize cannot exceed 100.");
    }

    private bool BeValidTagQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
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
