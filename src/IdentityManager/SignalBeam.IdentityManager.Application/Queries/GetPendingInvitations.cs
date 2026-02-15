using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Queries;

/// <summary>
/// Query to get pending invitations for a tenant.
/// </summary>
public record GetPendingInvitationsQuery(Guid TenantId);

/// <summary>
/// Invitation DTO.
/// </summary>
public record InvitationDto(
    Guid InvitationId,
    string Email,
    UserRole Role,
    InvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsExpired);

/// <summary>
/// Handler for getting pending invitations.
/// </summary>
public class GetPendingInvitationsHandler
{
    private readonly IUserInvitationRepository _invitationRepository;

    public GetPendingInvitationsHandler(IUserInvitationRepository invitationRepository)
    {
        _invitationRepository = invitationRepository;
    }

    public async Task<Result<List<InvitationDto>>> Handle(
        GetPendingInvitationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = new TenantId(query.TenantId);
        var invitations = await _invitationRepository.GetPendingByTenantAsync(tenantId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var dtos = invitations
            .Select(i => new InvitationDto(
                i.Id.Value,
                i.Email,
                i.Role,
                i.Status,
                i.CreatedAt,
                i.ExpiresAt,
                i.IsExpired(now)))
            .ToList();

        return Result.Success(dtos);
    }
}
