using FluentValidation;
using SignalBeam.BundleOrchestrator.Application.Commands;

namespace SignalBeam.BundleOrchestrator.Application.Validators;

public class ReportReconciliationStatusValidator : AbstractValidator<ReportReconciliationStatusCommand>
{
    public ReportReconciliationStatusValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required.")
            .WithErrorCode("DEVICE_ID_REQUIRED");

        RuleFor(x => x.Status)
            .Must(s => string.Equals(s, ReportReconciliationStatusHandler.Succeeded, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, ReportReconciliationStatusHandler.Failed, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status must be 'succeeded' or 'failed'.")
            .WithErrorCode("INVALID_RECONCILIATION_STATUS");

        RuleFor(x => x.BundleId)
            .MaximumLength(100)
            .WithMessage("BundleId cannot exceed 100 characters.")
            .WithErrorCode("INVALID_BUNDLE_ID");

        RuleFor(x => x.BundleVersion)
            .MaximumLength(100)
            .WithMessage("BundleVersion cannot exceed 100 characters.")
            .WithErrorCode("INVALID_VERSION");

        RuleFor(x => x.Errors)
            .Must(e => e is null || e.Count <= 50)
            .WithMessage("At most 50 errors can be reported.")
            .WithErrorCode("TOO_MANY_ERRORS");

        RuleForEach(x => x.Errors)
            .MaximumLength(1000)
            .WithMessage("Each error cannot exceed 1000 characters.")
            .WithErrorCode("ERROR_TOO_LONG");
    }
}
