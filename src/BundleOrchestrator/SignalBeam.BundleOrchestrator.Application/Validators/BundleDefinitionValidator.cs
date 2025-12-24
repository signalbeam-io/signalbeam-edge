using FluentValidation;
using SignalBeam.BundleOrchestrator.Application.Models;
using System.Text.RegularExpressions;

namespace SignalBeam.BundleOrchestrator.Application.Validators;

/// <summary>
/// Validator for bundle definitions.
/// </summary>
public partial class BundleDefinitionValidator : AbstractValidator<BundleDefinition>
{
    private static readonly string[] AllowedImageRegistries = new[]
    {
        "ghcr.io",
        "docker.io",
        "mcr.microsoft.com",
        "quay.io"
    };

    public BundleDefinitionValidator()
    {
        RuleFor(x => x.BundleId)
            .NotEmpty()
            .WithMessage("Bundle ID is required")
            .Must(BeValidBundleId)
            .WithMessage("Bundle ID must be a GUID or contain only lowercase letters, numbers, and hyphens");

        RuleFor(x => x.Version)
            .NotEmpty()
            .WithMessage("Version is required")
            .Matches(@"^\d+\.\d+\.\d+(?:-[a-zA-Z0-9]+)?$")
            .WithMessage("Version must be a valid semantic version (e.g., 1.2.3 or 1.2.3-beta)");

        RuleFor(x => x.Checksum)
            .Matches(@"^sha256:[a-f0-9]{64}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Checksum))
            .WithMessage("Checksum must be in format 'sha256:' followed by 64 hex characters");

        RuleFor(x => x.Containers)
            .NotEmpty()
            .WithMessage("At least one container is required")
            .Must(c => c.Count > 0)
            .WithMessage("Bundle must contain at least one container");

        RuleForEach(x => x.Containers)
            .SetValidator(new ContainerDefinitionValidator());
    }

    private static bool BeValidBundleId(string bundleId)
    {
        if (Guid.TryParse(bundleId, out _))
            return true;

        return Regex.IsMatch(bundleId, @"^[a-z0-9-]+$");
    }
}

/// <summary>
/// Validator for container definitions.
/// </summary>
public partial class ContainerDefinitionValidator : AbstractValidator<ContainerDefinition>
{
    private static readonly string[] AllowedImageRegistries = new[]
    {
        "ghcr.io",
        "docker.io",
        "mcr.microsoft.com",
        "quay.io",
        "localhost" // For local development
    };

    private static readonly string[] AllowedImagePullPolicies = new[]
    {
        "Always",
        "IfNotPresent",
        "Never"
    };

    private static readonly string[] AllowedRestartPolicies = new[]
    {
        "always",
        "on-failure",
        "unless-stopped",
        "no"
    };

    public ContainerDefinitionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Container name is required")
            .Matches(@"^[a-z0-9-]+$")
            .WithMessage("Container name must contain only lowercase letters, numbers, and hyphens");

        RuleFor(x => x.Image)
            .NotEmpty()
            .WithMessage("Container image is required")
            .Must(BeValidImageReference)
            .WithMessage($"Container image must be from allowed registries: {string.Join(", ", AllowedImageRegistries)}");

        RuleFor(x => x.ImagePullPolicy)
            .Must(p => AllowedImagePullPolicies.Contains(p))
            .WithMessage($"Image pull policy must be one of: {string.Join(", ", AllowedImagePullPolicies)}");

        RuleFor(x => x.RestartPolicy)
            .Must(p => AllowedRestartPolicies.Contains(p))
            .WithMessage($"Restart policy must be one of: {string.Join(", ", AllowedRestartPolicies)}");

        // Validate ports if present
        When(x => x.Ports != null && x.Ports.Count > 0, () =>
        {
            RuleForEach(x => x.Ports)
                .SetValidator(new PortMappingValidator());
        });

        // Validate resources if present
        When(x => x.Resources != null, () =>
        {
            RuleFor(x => x.Resources)
                .SetValidator(new ResourceRequirementsValidator()!);
        });

        // Validate volumes if present
        When(x => x.Volumes != null && x.Volumes.Count > 0, () =>
        {
            RuleForEach(x => x.Volumes)
                .SetValidator(new VolumeMountValidator());
        });
    }

    private bool BeValidImageReference(string image)
    {
        if (string.IsNullOrWhiteSpace(image))
            return false;

        // Extract registry from image reference
        // Format: [registry/][repo/]image[:tag]
        var parts = image.Split('/');
        if (parts.Length == 0)
            return false;

        var registry = parts.Length > 2 ? parts[0] : "docker.io";

        return AllowedImageRegistries.Any(allowed =>
            registry.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
            registry.StartsWith(allowed + ":", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Validator for port mappings.
/// </summary>
public class PortMappingValidator : AbstractValidator<PortMapping>
{
    public PortMappingValidator()
    {
        RuleFor(x => x.ContainerPort)
            .InclusiveBetween(1, 65535)
            .WithMessage("Container port must be between 1 and 65535");

        RuleFor(x => x.HostPort)
            .InclusiveBetween(1, 65535)
            .WithMessage("Host port must be between 1 and 65535");

        RuleFor(x => x.Protocol)
            .Must(p => p == "tcp" || p == "udp")
            .WithMessage("Protocol must be 'tcp' or 'udp'");
    }
}

/// <summary>
/// Validator for resource requirements.
/// </summary>
public class ResourceRequirementsValidator : AbstractValidator<ResourceRequirements>
{
    public ResourceRequirementsValidator()
    {
        When(x => x.Limits != null, () =>
        {
            RuleFor(x => x.Limits)
                .SetValidator(new ResourceSpecValidator()!);
        });

        When(x => x.Requests != null, () =>
        {
            RuleFor(x => x.Requests)
                .SetValidator(new ResourceSpecValidator()!);
        });
    }
}

/// <summary>
/// Validator for resource specifications.
/// </summary>
public partial class ResourceSpecValidator : AbstractValidator<ResourceSpec>
{
    public ResourceSpecValidator()
    {
        When(x => !string.IsNullOrWhiteSpace(x.Memory), () =>
        {
            RuleFor(x => x.Memory)
                .Matches(MemoryPattern())
                .WithMessage("Memory must be in format like '128Mi', '1Gi', etc.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Cpu), () =>
        {
            RuleFor(x => x.Cpu)
                .Matches(@"^\d+(\.\d+)?$")
                .WithMessage("CPU must be a decimal number (e.g., '0.5', '1', '2')");
        });
    }

    [GeneratedRegex(@"^\d+(\.\d+)?(Mi|Gi|Ki|M|G|K)$")]
    private static partial Regex MemoryPattern();
}

/// <summary>
/// Validator for volume mounts.
/// </summary>
public class VolumeMountValidator : AbstractValidator<VolumeMount>
{
    public VolumeMountValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Volume name is required");

        RuleFor(x => x.HostPath)
            .NotEmpty()
            .WithMessage("Host path is required");

        RuleFor(x => x.ContainerPath)
            .NotEmpty()
            .WithMessage("Container path is required");
    }
}
