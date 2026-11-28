using System.Text.Json;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Application.Services;

// A container as the edge agent runs it (ContainerSpec in SignalBeam.EdgeAgent.Application).
// Ports map "{containerPort}/{protocol}" to the host port; volumes map host path to container
// path, with ":ro" appended for read-only mounts.
public record DesiredContainerDto(
    string Name,
    string Image,
    Dictionary<string, string>? Environment,
    Dictionary<string, string>? Ports,
    Dictionary<string, string>? Volumes);

// Bundle versions store env, ports and volumes as JSON in two shapes: CreateBundleVersion writes
// ports as "host:container[/protocol]" strings, UploadBundleVersion writes PortMapping and
// VolumeMount objects. Both are accepted; anything unreadable is left out rather than failing the
// whole desired state.
public static class DesiredContainerMapper
{
    public static DesiredContainerDto Map(ContainerSpec spec) => new(
        spec.Name,
        spec.Image,
        MapEnvironment(spec.EnvironmentVariables),
        MapPorts(spec.PortMappings),
        MapVolumes(spec.VolumeMounts));

    public static Dictionary<string, string>? MapEnvironment(string? json) =>
        Read(json, root =>
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var environment = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                environment[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()!
                    : property.Value.GetRawText();
            }

            return environment;
        });

    public static Dictionary<string, string>? MapPorts(string? json) =>
        Read(json, root =>
        {
            if (root.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var ports = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in root.EnumerateArray())
            {
                var mapped = item.ValueKind switch
                {
                    JsonValueKind.String => ParsePortString(item.GetString()),
                    JsonValueKind.Object => ParsePortObject(item),
                    _ => null
                };

                if (mapped is { } port)
                {
                    ports[port.Key] = port.HostPort;
                }
            }

            return ports;
        });

    public static Dictionary<string, string>? MapVolumes(string? json) =>
        Read(json, root =>
        {
            if (root.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var volumes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var parts = item.GetString()?.Split(':', 2);
                    if (parts is [{ Length: > 0 } host, { Length: > 0 } container])
                    {
                        volumes[host] = container;
                    }

                    continue;
                }

                if (item.ValueKind != JsonValueKind.Object
                    || GetString(item, "hostPath") is not { Length: > 0 } hostPath
                    || GetString(item, "containerPath") is not { Length: > 0 } containerPath)
                {
                    continue;
                }

                var readOnly = TryGetProperty(item, "readOnly", out var flag) && flag.ValueKind == JsonValueKind.True;
                volumes[hostPath] = readOnly ? $"{containerPath}:ro" : containerPath;
            }

            return volumes;
        });

    // "8080:80", "8080:80/udp" or "80" (container port only, host port chosen by Docker).
    private static (string Key, string HostPort)? ParsePortString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var protocol = "tcp";
        var slash = value.IndexOf('/');
        if (slash >= 0)
        {
            protocol = value[(slash + 1)..].Trim().ToLowerInvariant();
            value = value[..slash];
        }

        var parts = value.Split(':');
        var (host, container) = parts.Length switch
        {
            1 => (string.Empty, parts[0]),
            2 => (parts[0], parts[1]),
            _ => (parts[^2], parts[^1]) // "ip:host:container": the agent binds host ports only
        };

        return int.TryParse(container.Trim(), out var containerPort) && containerPort > 0
            ? ($"{containerPort}/{protocol}", host.Trim())
            : null;
    }

    private static (string Key, string HostPort)? ParsePortObject(JsonElement item)
    {
        if (GetPort(item, "containerPort") is not { } container)
        {
            return null;
        }

        var host = GetPort(item, "hostPort")?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        var protocol = GetString(item, "protocol") is { Length: > 0 } p ? p.ToLowerInvariant() : "tcp";
        return ($"{container}/{protocol}", host);
    }

    private static Dictionary<string, string>? Read(string? json, Func<JsonElement, Dictionary<string, string>?> map)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return map(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? GetPort(JsonElement item, string name) =>
        TryGetProperty(item, name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var port)
        && port > 0
            ? port
            : null;

    private static string? GetString(JsonElement item, string name) =>
        TryGetProperty(item, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool TryGetProperty(JsonElement item, string name, out JsonElement value)
    {
        foreach (var property in item.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
