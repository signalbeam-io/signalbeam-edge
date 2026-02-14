using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Commands;

/// <summary>
/// Command to update a user's profile information.
/// </summary>
public record UpdateUserProfileCommand(string ZitadelUserId, string Name);

/// <summary>
/// Handler for updating user profile.
/// </summary>
public class UpdateUserProfileHandler
{
    private readonly IUserRepository _userRepository;

    public UpdateUserProfileHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result> Handle(
        UpdateUserProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 200)
        {
            return Result.Failure(
                Error.Validation("INVALID_NAME", "Name must be between 1 and 200 characters."));
        }

        var user = await _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(
                Error.NotFound("USER_NOT_FOUND", "User not found."));
        }

        user.UpdateProfile(command.Name.Trim());
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
