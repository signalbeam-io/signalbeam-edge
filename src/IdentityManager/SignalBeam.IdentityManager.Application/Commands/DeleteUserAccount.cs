using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Commands;

/// <summary>
/// Command to soft-delete a user account.
/// </summary>
public record DeleteUserAccountCommand(string ZitadelUserId);

/// <summary>
/// Handler for deleting a user account.
/// </summary>
public class DeleteUserAccountHandler
{
    private readonly IUserRepository _userRepository;

    public DeleteUserAccountHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result> Handle(
        DeleteUserAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(
                Error.NotFound("USER_NOT_FOUND", "User not found."));
        }

        user.Delete();
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
