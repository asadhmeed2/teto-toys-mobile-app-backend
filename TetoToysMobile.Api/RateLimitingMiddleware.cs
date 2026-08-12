using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Per-IP token bucket, held in Redis so every instance shares one budget.
///
/// Each client gets a bucket of <see cref="RateLimitSettings.GlobalBurst"/> tokens that
/// refills continuously at Limit/WindowSeconds tokens per second. A request spends one
/// token; if the bucket is empty the request is rejected.
///
/// Chosen over a fixed window because a fixed window lets a client spend its whole
/// budget at the end of one window and again at the start of the next — a 2x burst at
/// the boundary. A bucket smooths that out: burst size is capped by capacity and the
/// long-run average can never exceed the refill rate.
/// </summary>
public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimit";

    public bool Enabled { get; set; } = true;

    /// <summary>Scopes buckets per service so one API's traffic can't exhaust another's budget.</summary>
    public string ServiceName { get; set; } = "mobile";

    /// <summary>Sustained rate: GlobalLimit tokens per GlobalWindowSeconds.</summary>
    public int GlobalLimit { get; set; } = 100;
    public int GlobalWindowSeconds { get; set; } = 60;

    /// <summary>Bucket capacity — the largest instantaneous burst. Falls back to GlobalLimit when unset.</summary>
    public int GlobalBurst { get; set; }

    /// <summary>Login/refresh/register/reset are what actually get brute-forced.</summary>
    public int AuthLimit { get; set; } = 10;
    public int AuthWindowSeconds { get; set; } = 60;
    public int AuthBurst { get; set; }

    public string[] StrictPathPrefixes { get; set; } = ["/api/auth"];

    /// <summary>
    /// Only enable behind a proxy you control. X-Forwarded-For is client-settable,
    /// so trusting it on a directly-exposed service lets anyone forge their identity
    /// and sidestep the limit entirely.
    /// </summary>
    public bool TrustForwardedHeaders { get; set; }

    public int ResolvedGlobalBurst => GlobalBurst > 0 ? GlobalBurst : GlobalLimit;
    public int ResolvedAuthBurst => AuthBurst > 0 ? AuthBurst : AuthLimit;
}

public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitSettings _settings;
    private readonly IConnectionMultiplexer? _redis;

    /// <summary>
    /// Token bucket, evaluated atomically so concurrent requests can't both read the
    /// same token count and each decide they may proceed.
    ///
    /// Time comes from redis.call('TIME'), not the caller: instances may have skewed
    /// clocks, and the bucket must advance on a single shared timeline. Safe to
    /// replicate since Redis 5 propagates script *effects* rather than the script.
    ///
    /// Must stay byte-identical to the node and flask copies so all services agree.
    /// </summary>
    private const string TokenBucketScript = @"
local key      = KEYS[1]
local capacity = tonumber(ARGV[1])
local refill   = tonumber(ARGV[2])
local wanted   = tonumber(ARGV[3])

local t   = redis.call('TIME')
local now = tonumber(t[1]) + (tonumber(t[2]) / 1000000)

local bucket = redis.call('HMGET', key, 'tokens', 'ts')
local tokens = tonumber(bucket[1])
local ts     = tonumber(bucket[2])

if tokens == nil or ts == nil then
  tokens = capacity
  ts     = now
end

local elapsed = now - ts
if elapsed > 0 then
  tokens = math.min(capacity, tokens + (elapsed * refill))
end

local allowed = 0
if tokens >= wanted then
  tokens  = tokens - wanted
  allowed = 1
end

redis.call('HSET', key, 'tokens', tokens, 'ts', now)
-- Reclaim idle buckets once they would have refilled completely.
redis.call('EXPIRE', key, math.ceil(capacity / refill) + 1)

local retry = 0
if allowed == 0 then
  retry = math.ceil((wanted - tokens) / refill)
end

return { allowed, math.floor(tokens), retry }
";

    public RateLimitingMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _settings = configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>()
                    ?? new RateLimitSettings();
        _redis = serviceProvider.GetService<IConnectionMultiplexer>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // CORS preflights are issued by the browser, not the caller — charging them
        // would halve every cross-origin client's effective budget.
        if (!_settings.Enabled || _redis == null || HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        var isStrict = _settings.StrictPathPrefixes
            .Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        var limit = isStrict ? _settings.AuthLimit : _settings.GlobalLimit;
        var window = Math.Max(1, isStrict ? _settings.AuthWindowSeconds : _settings.GlobalWindowSeconds);
        var capacity = Math.Max(1, isStrict ? _settings.ResolvedAuthBurst : _settings.ResolvedGlobalBurst);
        var scope = isStrict ? "auth" : "global";

        // Tokens per second. Guarded so a misconfigured 0 limit can't divide by zero.
        var refillPerSecond = Math.Max(0.0001, (double)limit / window);

        var key = $"ratelimit:{_settings.ServiceName}:{scope}:{ResolveClientId(context)}";

        bool allowed;
        long remaining;
        int retryAfter;

        try
        {
            var result = (RedisResult[])(await _redis.GetDatabase().ScriptEvaluateAsync(
                TokenBucketScript,
                [key],
                [capacity, refillPerSecond, 1]))!;

            allowed = (long)result[0] == 1;
            remaining = (long)result[1];
            retryAfter = Math.Max(1, (int)(long)result[2]);
        }
        catch
        {
            // Fail open. A Redis outage must degrade to "unlimited", never to "down".
            await _next(context);
            return;
        }

        context.Response.Headers["X-RateLimit-Limit"] = capacity.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, remaining).ToString();

        if (!allowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = retryAfter.ToString();
            context.Response.Headers["X-RateLimit-Reset"] = retryAfter.ToString();
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "rate_limited",
                error_description = $"Too many requests. Please try again in {retryAfter} seconds.",
            }));
            return;
        }

        await _next(context);
    }

    private string ResolveClientId(HttpContext context)
    {
        if (_settings.TrustForwardedHeaders)
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                // Left-most entry is the original client.
                return forwarded.Split(',')[0].Trim();
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

public static class RateLimitingMiddlewareExtensions
{
    /// <summary>Register early — before auth and endpoints — so rejected traffic costs least.</summary>
    public static IApplicationBuilder UseRedisRateLimiting(this IApplicationBuilder app) =>
        app.UseMiddleware<RateLimitingMiddleware>();
}
