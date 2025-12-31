using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Device group for logical grouping and rollout management.
/// Supports both static (manual membership) and dynamic (tag-based membership) groups.
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
    /// Type of device group (Static or Dynamic).
    /// </summary>
    public GroupType Type { get; private set; }

    /// <summary>
    /// Tag query expression for dynamic groups.
    /// Example: "environment=production AND location=warehouse-*"
    /// Required for Dynamic groups, must be null for Static groups.
    /// </summary>
    public string? TagQuery { get; private set; }

    /// <summary>
    /// When the group was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Tag-based selection criteria (devices with these tags belong to this group).
    /// DEPRECATED: Use TagQuery instead for dynamic groups.
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
    /// Factory method to create a new static device group (manual membership).
    /// </summary>
    public static DeviceGroup CreateStatic(
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
            Description = description,
            Type = GroupType.Static,
            TagQuery = null
        };
    }

    /// <summary>
    /// Factory method to create a new dynamic device group (tag-based membership).
    /// </summary>
    public static DeviceGroup CreateDynamic(
        DeviceGroupId id,
        TenantId tenantId,
        string name,
        string? description,
        string tagQuery,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Device group name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(tagQuery))
            throw new ArgumentException("Dynamic groups must have a tag query.", nameof(tagQuery));

        return new DeviceGroup(id, tenantId, name, createdAt)
        {
            Description = description,
            Type = GroupType.Dynamic,
            TagQuery = tagQuery
        };
    }

    /// <summary>
    /// Factory method to create a new device group (backward compatibility).
    /// Creates a static group by default.
    /// </summary>
    [Obsolete("Use CreateStatic or CreateDynamic instead.")]
    public static DeviceGroup Create(
        DeviceGroupId id,
        TenantId tenantId,
        string name,
        string? description,
        DateTimeOffset createdAt)
    {
        return CreateStatic(id, tenantId, name, description, createdAt);
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
    /// Updates the tag query for dynamic groups.
    /// </summary>
    /// <param name="tagQuery">Tag query expression (e.g., "environment=production AND location=warehouse-*")</param>
    /// <exception cref="InvalidOperationException">Thrown if called on a static group</exception>
    /// <exception cref="ArgumentException">Thrown if tag query is null or empty for dynamic groups</exception>
    public void UpdateTagQuery(string? tagQuery)
    {
        if (Type == GroupType.Static)
        {
            throw new InvalidOperationException("Cannot set tag query on static groups.");
        }

        if (string.IsNullOrWhiteSpace(tagQuery))
        {
            throw new ArgumentException("Dynamic groups must have a tag query.", nameof(tagQuery));
        }

        TagQuery = tagQuery;
    }

    /// <summary>
    /// Adds a tag to the selection criteria.
    /// DEPRECATED: Use UpdateTagQuery instead for dynamic groups.
    /// </summary>
    [Obsolete("Use UpdateTagQuery instead for dynamic groups.")]
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
    /// DEPRECATED: Use UpdateTagQuery instead for dynamic groups.
    /// </summary>
    [Obsolete("Use UpdateTagQuery instead for dynamic groups.")]
    public void RemoveTagCriterion(string tag)
    {
        _tagCriteria.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
    }
}
