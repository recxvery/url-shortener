using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using UrlShortener.Application.Abstractions;

namespace UrlShortener.Infrastructure.Caching;

public sealed class RedisLinkCache : ILinkCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisLinkCache> _logger;

    public RedisLinkCache(IDistributedCache cache, ILogger<RedisLinkCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<CachedLink?> GetAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await _cache.GetAsync(Key(code), cancellationToken);
            return payload is null
                ? null
                : JsonSerializer.Deserialize<CachedLink>(payload, SerializerOptions);
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Redis read failed for code {Code}, falling back to PostgreSQL", code);
            return null;
        }
    }

    public async Task SetAsync(string code, CachedLink link, TimeSpan ttl, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(link, SerializerOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };

            await _cache.SetAsync(Key(code), payload, options, cancellationToken);
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Redis write failed for code {Code}", code);
        }
    }

    public async Task RemoveAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            await _cache.RemoveAsync(Key(code), cancellationToken);
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Redis delete failed for code {Code}", code);
        }
    }

    private static string Key(string code)
    {
        return $"link:{code}";
    }
}
