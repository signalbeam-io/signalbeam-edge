using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SignalBeam.Domain.Queries.TagQuery;

namespace SignalBeam.DeviceManager.Infrastructure.Caching;

/// <summary>
/// Cache for parsed tag query expressions.
/// Improves performance by avoiding re-parsing the same queries.
/// </summary>
public class TagQueryCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TagQueryCache> _logger;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    public TagQueryCache(IMemoryCache cache, ILogger<TagQueryCache> logger)
    {
        _cache = cache;
        _logger = logger;

        // Cache entries for 1 hour with sliding expiration of 15 minutes
        _cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            SlidingExpiration = TimeSpan.FromMinutes(15),
            Size = 1 // For memory cache size limit
        };
    }

    /// <summary>
    /// Gets a parsed query expression from cache or parses and caches it.
    /// </summary>
    /// <param name="queryString">Tag query string</param>
    /// <returns>Parsed query expression or null if parsing fails</returns>
    public TagQueryExpression? GetOrParse(string queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return null;
        }

        // Normalize query string for consistent caching
        var normalizedQuery = queryString.Trim().ToLowerInvariant();
        var cacheKey = $"TagQuery:{normalizedQuery}";

        // Try to get from cache
        if (_cache.TryGetValue<TagQueryExpression>(cacheKey, out var cachedQuery) && cachedQuery is not null)
        {
            _logger.LogDebug("Tag query cache hit for: {Query}", queryString);
            return cachedQuery;
        }

        // Parse and cache
        try
        {
            var parsedQuery = TagQueryParser.Parse(queryString);

            _cache.Set(cacheKey, parsedQuery, _cacheOptions);

            _logger.LogDebug("Parsed and cached tag query: {Query}", queryString);

            return parsedQuery;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse tag query: {Query}", queryString);
            return null;
        }
    }

    /// <summary>
    /// Clears all cached queries.
    /// </summary>
    public void Clear()
    {
        // IMemoryCache doesn't have a clear method, so we'd need to track keys
        // For now, entries will expire naturally
        _logger.LogInformation("Tag query cache clear requested (entries will expire naturally)");
    }

    /// <summary>
    /// Removes a specific query from the cache.
    /// </summary>
    /// <param name="queryString">Tag query string to remove</param>
    public void Remove(string queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return;
        }

        var normalizedQuery = queryString.Trim().ToLowerInvariant();
        var cacheKey = $"TagQuery:{normalizedQuery}";

        _cache.Remove(cacheKey);

        _logger.LogDebug("Removed tag query from cache: {Query}", queryString);
    }
}
