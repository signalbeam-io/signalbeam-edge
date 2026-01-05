using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

/// <summary>
/// Query to get registration tokens for a tenant.
/// </summary>
public record GetRegistrationTokensQuery(
    Guid TenantId,
    bool IncludeInactive = false,
    int PageNumber = 1,
    int PageSize = 50);

/// <summary>
/// Response for a single registration token (without the actual token hash).
/// </summary>
public record RegistrationTokenDto(
    Guid Id,
    Guid TenantId,
    string Token,
    DateTimeOffset? ExpiresAt,
    int? MaxUses,
    int CurrentUses,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    bool IsActive);

/// <summary>
/// Paginated response for registration tokens.
/// </summary>
public record GetRegistrationTokensResponse(
    IReadOnlyList<RegistrationTokenDto> Data,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

/// <summary>
/// Handler for GetRegistrationTokensQuery.
/// </summary>
public class GetRegistrationTokensHandler
{
    private readonly IDeviceRegistrationTokenRepository _tokenRepository;
    private readonly ILogger<GetRegistrationTokensHandler> _logger;

    public GetRegistrationTokensHandler(
        IDeviceRegistrationTokenRepository tokenRepository,
        ILogger<GetRegistrationTokensHandler> logger)
    {
        _tokenRepository = tokenRepository;
        _logger = logger;
    }

    public async Task<Result<GetRegistrationTokensResponse>> Handle(
        GetRegistrationTokensQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = new TenantId(query.TenantId);

            // Get all tokens for tenant
            var allTokens = await _tokenRepository.GetAllByTenantAsync(
                tenantId,
                query.IncludeInactive,
                cancellationToken);

            // Apply pagination
            var totalCount = allTokens.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

            var paginatedTokens = allTokens
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(t => new RegistrationTokenDto(
                    t.Id,
                    t.TenantId.Value,
                    t.TokenPrefix, // Only show prefix, not full token
                    t.ExpiresAt,
                    t.MaxUses,
                    t.CurrentUses,
                    t.CreatedAt,
                    t.CreatedBy,
                    t.IsActive))
                .ToList();

            var response = new GetRegistrationTokensResponse(
                paginatedTokens,
                totalCount,
                query.PageNumber,
                query.PageSize,
                totalPages);

            return Result<GetRegistrationTokensResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get registration tokens for tenant {TenantId}",
                query.TenantId);

            return Result.Failure<GetRegistrationTokensResponse>(
                Error.Unexpected(
                    "RegistrationTokens.QueryFailed",
                    "Failed to retrieve registration tokens."));
        }
    }
}
