using StackExchange.Redis;
using TetoToysMobile.Domain.Interfaces;

namespace TetoToysMobile.Infrastructure.Caching;

public class RedisCacheService : IRedisCacheService
{
    private readonly IConnectionMultiplexer _multiplexer;

    public RedisCacheService(IConnectionMultiplexer multiplexer)
    {
        _multiplexer = multiplexer;
    }

    // Key format shared with the other backends: the key IS the token, so its
    // presence proves this platform issued it and it hasn't been revoked.
    private static string RefreshKey(string token) => $"refresh:{token}";

    public async Task SetRefreshTokenAsync(string token, TimeSpan ttl) =>
        await _multiplexer.GetDatabase().StringSetAsync(RefreshKey(token), "1", ttl);

    public async Task<bool> ValidateRefreshTokenAsync(string token) =>
        await _multiplexer.GetDatabase().KeyExistsAsync(RefreshKey(token));

    public async Task InvalidateRefreshTokenAsync(string token) =>
        await _multiplexer.GetDatabase().KeyDeleteAsync(RefreshKey(token));

    public async Task<string?> GetStringAsync(string key)
    {
        var value = await _multiplexer.GetDatabase().StringGetAsync(key);
        // Cast explicitly: RedisValue converts implicitly to both string and
        // ReadOnlySpan<byte>, which leaves some overloads ambiguous.
        return value.HasValue ? (string?)value : null;
    }

    public async Task SetStringAsync(string key, string value, TimeSpan ttl) =>
        await _multiplexer.GetDatabase().StringSetAsync(key, value, ttl);
}
