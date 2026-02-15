using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Queries;

/// <summary>
/// Query to get paginated team members for a tenant.
/// </summary>
public record GetTenantUsersQuery(
    Guid TenantId,
    int Page = 1,
    int PageSize = 20,
    string? Search = null);

/// <summary>
/// Team member DTO for the team directory.
/// </summary>
public record TeamMemberDto(
    Guid UserId,
    string Email,
    string Name,
    UserRole Role,
    UserStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

/// <summary>
/// Paginated response for team members.
/// </summary>
public record PaginatedTeamResponse(
    List<TeamMemberDto> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>
/// Handler for getting tenant users query.
/// </summary>
public class GetTenantUsersHandler
{
    private readonly IUserRepository _userRepository;

    public GetTenantUsersHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<PaginatedTeamResponse>> Handle(
        GetTenantUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = new TenantId(query.TenantId);
        var users = await _userRepository.GetByTenantAsync(tenantId, cancellationToken);

        // Filter out deleted users
        var activeUsers = users.Where(u => u.Status != UserStatus.Deleted).ToList();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            activeUsers = activeUsers
                .Where(u => u.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || u.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var total = activeUsers.Count;
        var totalPages = (int)Math.Ceiling(total / (double)query.PageSize);

        var pagedUsers = activeUsers
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new TeamMemberDto(
                u.Id.Value,
                u.Email,
                u.Name,
                u.Role,
                u.Status,
                u.CreatedAt,
                u.LastLoginAt))
            .ToList();

        return Result.Success(new PaginatedTeamResponse(
            pagedUsers, total, query.Page, query.PageSize, totalPages));
    }
}
