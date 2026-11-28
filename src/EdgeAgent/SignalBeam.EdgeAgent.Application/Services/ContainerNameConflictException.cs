namespace SignalBeam.EdgeAgent.Application.Services;

// A bundle container's name is taken by a container the agent didn't create (for example one
// started by an older agent, before containers were labelled). The agent leaves it alone.
public sealed class ContainerNameConflictException(string containerName)
    : InvalidOperationException(Describe(containerName))
{
    public const string ErrorCode = "CONTAINER_NAME_CONFLICT";

    public string ContainerName { get; } = containerName;

    // The form reported to the cloud, so the conflict can be told apart from other failures.
    public static string Describe(string containerName) =>
        $"{ErrorCode}: a container named '{containerName}' already exists and was not created by SignalBeam; it was left untouched.";
}
