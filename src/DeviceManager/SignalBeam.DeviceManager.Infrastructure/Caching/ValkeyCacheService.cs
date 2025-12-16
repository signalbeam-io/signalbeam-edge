using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SignalBeam.DeviceManager.Infrastructure.Caching;

/// <summary>
/// Valkey (Redis-compatible) cache service implementation.
/// Provides distributed caching capabilities.
/// </summary>
public class ValkeyCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ValkeyCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ValkeyCacheService(
        IConnectionMultiplexer redis,
        ILogger<ValkeyCacheService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return null;
            }

            _logger.LogDebug("Cache hit for key: {Key}", key);
            return JsonSerializer.Deserialize<T>(value.ToString(), _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache value for key: {Key}", key);
            return null; // Fail gracefully
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        try
        {
            var db = _redis.GetDatabase();
            var serialized = JsonSerializer.Serialize(value, _jsonOptions);

            await db.StringSetAsync(key, serialized, expiration);

            _logger.LogDebug(
                "Set cache value for key: {Key} with expiration: {Expiration}",
                key,
                expiration?.ToString() ?? "none");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set cache value for key: {Key}", key);
            // Don't throw - caching failures shouldn't break the application
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);

            _logger.LogDebug("Removed cache key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove cache key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        try
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence of cache key: {Key}", key);
            return false;
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        await SetAsync(key, value, expiration, cancellationToken);

        return value;
    }
}

/// <summary>
/// Interface for cache service operations.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class;

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
        where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
        where T : class;
}
