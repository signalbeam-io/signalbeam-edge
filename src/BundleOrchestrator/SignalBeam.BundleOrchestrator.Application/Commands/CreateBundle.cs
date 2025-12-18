using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to create a new app bundle.
/// </summary>
public record CreateBundleCommand(
    Guid TenantId,
    string Name,
    string? Description = null);

/// <summary>
/// Response after creating a bundle.
/// </summary>
public record CreateBundleResponse(
    Guid BundleId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt);

/// <summary>
/// Handler for CreateBundleCommand.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class CreateBundleHandler
{
    private readonly IBundleRepository _bundleRepository;

    public CreateBundleHandler(IBundleRepository bundleRepository)
    {
        _bundleRepository = bundleRepository;
    }

    public async Task<Result<CreateBundleResponse>> Handle(
        CreateBundleCommand command,
        CancellationToken cancellationToken)
    {
        // Validate tenant ID
        var tenantId = new TenantId(command.TenantId);

        // Check if bundle with same name already exists for this tenant
        var existingBundle = await _bundleRepository.GetByNameAsync(tenantId, command.Name, cancellationToken);
        if (existingBundle is not null)
        {
            var error = Error.Conflict(
                "BUNDLE_ALREADY_EXISTS",
                $"Bundle with name '{command.Name}' already exists for tenant {command.TenantId}.");
            return Result.Failure<CreateBundleResponse>(error);
        }

        // Create new bundle using factory method
        var bundleId = new BundleId(Guid.NewGuid());
        var bundle = AppBundle.Create(
            bundleId,
            tenantId,
            command.Name,
            command.Description,
            DateTimeOffset.UtcNow);

        // Save to repository
        await _bundleRepository.AddAsync(bundle, cancellationToken);
        await _bundleRepository.SaveChangesAsync(cancellationToken);

        // Return response
        return Result<CreateBundleResponse>.Success(new CreateBundleResponse(
            bundle.Id.Value,
            bundle.Name,
            bundle.Description,
            bundle.CreatedAt));
    }
}
