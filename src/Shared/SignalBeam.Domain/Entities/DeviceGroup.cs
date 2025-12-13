using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Device group for logical grouping and rollout management.
/// </summary>
public class DeviceGroup : AggregateRoot<DeviceGroupId>
{
    /// <summary>
    /// Tenant this group belongs to.
    /// </summary>
    public TenantId TenantId { get; private set; }

    /// <summary>
    /// Name of the device group.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Description of the device group.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// When the group was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Tag-based selection criteria (devices with these tags belong to this group).
    /// If empty, devices must be explicitly assigned.
    /// </summary>
    private readonly List<string> _tagCriteria = [];
    public IReadOnlyCollection<string> TagCriteria => _tagCriteria.AsReadOnly();

    // EF Core constructor
    private DeviceGroup() : base()
    {
    }

    private DeviceGroup(
        DeviceGroupId id,
        TenantId tenantId,
        string name,
        DateTimeOffset createdAt) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Factory method to create a new device group.
    /// </summary>
    public static DeviceGroup Create(
        DeviceGroupId id,
        TenantId tenantId,
        string name,
        string? description,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Device group name cannot be empty.", nameof(name));

        return new DeviceGroup(id, tenantId, name, createdAt)
        {
            Description = description
        };
    }

    /// <summary>
    /// Updates the group name.
    /// </summary>
    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Device group name cannot be empty.", nameof(name));

        Name = name;
    }

    /// <summary>
    /// Updates the group description.
    /// </summary>
    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    /// <summary>
    /// Adds a tag to the selection criteria.
    /// </summary>
    public void AddTagCriterion(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be empty.", nameof(tag));

        if (!_tagCriteria.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            _tagCriteria.Add(tag.ToLowerInvariant());
        }
    }

    /// <summary>
    /// Removes a tag from the selection criteria.
    /// </summary>
    public void RemoveTagCriterion(string tag)
    {
        _tagCriteria.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
    }
}
