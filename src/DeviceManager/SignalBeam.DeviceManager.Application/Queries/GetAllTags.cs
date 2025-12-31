using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

/// <summary>
/// Query to get all unique tags across all devices for a tenant.
/// Returns tags with their usage counts.
/// </summary>
public record GetAllTagsQuery(Guid TenantId);

/// <summary>
/// Response containing all unique tags with counts.
/// </summary>
public record GetAllTagsResponse(
    IReadOnlyCollection<TagInfo> Tags,
    int TotalUniqueTags);

/// <summary>
/// Information about a tag and its usage.
/// </summary>
public record TagInfo(
    string Tag,
    int DeviceCount,
    bool IsKeyValue,
    string? Key = null,
    string? Value = null);

/// <summary>
/// Handler for GetAllTagsQuery.
/// </summary>
public class GetAllTagsHandler
{
    private readonly IDeviceQueryRepository _deviceRepository;

    public GetAllTagsHandler(IDeviceQueryRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<GetAllTagsResponse>> Handle(
        GetAllTagsQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(query.TenantId);

        // Get all devices for the tenant
        var (devices, _) = await _deviceRepository.GetDevicesAsync(
            query.TenantId,
            status: null,
            tag: null,
            deviceGroupId: null,
            pageNumber: 1,
            pageSize: int.MaxValue,
            cancellationToken);

        // Collect all tags and count occurrences
        var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var device in devices)
        {
            foreach (var tag in device.Tags)
            {
                var normalizedTag = tag.ToLowerInvariant();
                if (tagCounts.ContainsKey(normalizedTag))
                {
                    tagCounts[normalizedTag]++;
                }
                else
                {
                    tagCounts[normalizedTag] = 1;
                }
            }
        }

        // Parse tags to extract key-value information
        var tagInfoList = new List<TagInfo>();

        foreach (var (tag, count) in tagCounts.OrderByDescending(kvp => kvp.Value))
        {
            try
            {
                var deviceTag = SignalBeam.Domain.ValueObjects.DeviceTag.Create(tag);

                tagInfoList.Add(new TagInfo(
                    Tag: tag,
                    DeviceCount: count,
                    IsKeyValue: deviceTag.IsKeyValue,
                    Key: deviceTag.IsKeyValue ? deviceTag.Key : null,
                    Value: deviceTag.IsKeyValue ? deviceTag.Value : null));
            }
            catch (ArgumentException)
            {
                // Invalid tag format, include as-is without parsing
                tagInfoList.Add(new TagInfo(
                    Tag: tag,
                    DeviceCount: count,
                    IsKeyValue: false));
            }
        }

        var response = new GetAllTagsResponse(
            Tags: tagInfoList,
            TotalUniqueTags: tagInfoList.Count);

        return Result<GetAllTagsResponse>.Success(response);
    }
}
