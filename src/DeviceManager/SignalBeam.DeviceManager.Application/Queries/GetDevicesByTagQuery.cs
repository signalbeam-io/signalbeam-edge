using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Queries.TagQuery;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

/// <summary>
/// Query to search devices using tag query expression.
/// Supports complex queries like: "environment=production AND location=warehouse-*"
/// </summary>
public record GetDevicesByTagQueryQuery(
    Guid TenantId,
    string TagQueryString,
    int PageNumber = 1,
    int PageSize = 20);

/// <summary>
/// Paginated response containing devices matching the tag query.
/// </summary>
public record GetDevicesByTagQueryResponse(
    IReadOnlyCollection<DeviceResponse> Devices,
    string TagQuery,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

/// <summary>
/// Handler for GetDevicesByTagQueryQuery.
/// </summary>
public class GetDevicesByTagQueryHandler
{
    private readonly IDeviceQueryRepository _deviceRepository;

    public GetDevicesByTagQueryHandler(IDeviceQueryRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<GetDevicesByTagQueryResponse>> Handle(
        GetDevicesByTagQueryQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(query.TenantId);

        // Parse tag query expression
        TagQueryExpression parsedQuery;
        try
        {
            parsedQuery = TagQueryParser.Parse(query.TagQueryString);
        }
        catch (ArgumentException ex)
        {
            var error = Error.Validation(
                "INVALID_TAG_QUERY_SYNTAX",
                $"Invalid tag query syntax: {ex.Message}");
            return Result.Failure<GetDevicesByTagQueryResponse>(error);
        }
        catch (FormatException ex)
        {
            var error = Error.Validation(
                "INVALID_TAG_QUERY_FORMAT",
                $"Invalid tag query format: {ex.Message}");
            return Result.Failure<GetDevicesByTagQueryResponse>(error);
        }

        // Get all devices for the tenant (we'll filter in-memory)
        // Note: For large datasets, consider moving query evaluation to SQL
        var (allDevices, _) = await _deviceRepository.GetDevicesAsync(
            query.TenantId,
            status: null,
            tag: null,
            deviceGroupId: null,
            pageNumber: 1,
            pageSize: int.MaxValue,
            cancellationToken);

        // Filter devices using tag query evaluator
        var matchingDevices = allDevices
            .Where(device => TagQueryEvaluator.Evaluate(parsedQuery, device))
            .ToList();

        // Apply pagination
        var totalCount = matchingDevices.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var paginatedDevices = matchingDevices
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        // Map to response DTOs
        var deviceResponses = paginatedDevices.Select(device => new DeviceResponse(
            device.Id.Value,
            device.TenantId.Value,
            device.Name,
            device.Status.ToString(),
            device.LastSeenAt,
            device.RegisteredAt,
            device.Metadata,
            device.Tags,
            device.AssignedBundleId?.Value,
            device.BundleDeploymentStatus?.ToString(),
            device.DeviceGroupId?.Value)).ToList();

        var response = new GetDevicesByTagQueryResponse(
            deviceResponses,
            query.TagQueryString,
            totalCount,
            query.PageNumber,
            query.PageSize,
            totalPages);

        return Result<GetDevicesByTagQueryResponse>.Success(response);
    }
}
