using System.Text.Json;
using System.Text.Json.Serialization;
using TetoToysMobile.Domain.Interfaces;

public static class StoreHoursEndpoints
{
    // Shared with the admin API (which deletes this key on save) and the other
    // storefront backends. Keep the string identical across services.
    private const string CacheKey = "store_hours:all";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private const string DefaultTimeZone = "Asia/Jerusalem";

    public static void MapStoreHoursEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        // GET /api/store-hours — weekly schedule plus whether the shop is open now.
        group.MapGet("/store-hours", async (HttpContext context) =>
        {
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var cache = context.RequestServices.GetRequiredService<IRedisCacheService>();

            List<StoreHourRow>? days = null;

            // 1. Try the shared cache. Redis being down must not take the API with it.
            try
            {
                var cached = await cache.GetStringAsync(CacheKey);
                if (!string.IsNullOrEmpty(cached))
                    days = JsonSerializer.Deserialize<List<StoreHourRow>>(cached);
            }
            catch
            {
                // fall through to the database
            }

            // 2. Cache miss — read through and repopulate.
            if (days == null)
            {
                var repo = context.RequestServices.GetRequiredService<IStoreHoursRepository>();
                days = (await repo.GetAllAsync()).ConvertAll(d => new StoreHourRow
                {
                    DayOfWeek = d.DayOfWeek,
                    OpenTime = FormatTime(d.OpenTime),
                    CloseTime = FormatTime(d.CloseTime),
                    IsClosed = d.IsClosed,
                });

                try
                {
                    await cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(days), CacheTtl);
                }
                catch
                {
                    // caching is best-effort
                }
            }

            // 3. is_open_now is always recomputed — never cached, or a stale "true"
            //    could outlive closing time by up to the full TTL.
            var timeZoneId = config["Store:TimeZone"] ?? DefaultTimeZone;
            var isOpenNow = ComputeIsOpenNow(days, timeZoneId, out var localNow, out var tzResolved);

            return Results.Ok(new
            {
                timezone = timeZoneId,
                // false => the host lacks tzdata and the times are server-local.
                timezone_resolved = tzResolved,
                server_time = localNow.ToString("yyyy-MM-ddTHH:mm:ss"),
                is_open_now = isOpenNow,
                days = days.ConvertAll(d => new
                {
                    day_of_week = d.DayOfWeek,
                    open_time = d.OpenTime,
                    close_time = d.CloseTime,
                    is_closed = d.IsClosed,
                }),
            });
        });
    }

    private static bool ComputeIsOpenNow(
        List<StoreHourRow> days, string timeZoneId, out DateTime localNow, out bool timeZoneResolved)
    {
        localNow = ResolveNow(timeZoneId, out timeZoneResolved);

        // .NET DayOfWeek already maps Sunday = 0 .. Saturday = 6.
        var row = days.Find(d => d.DayOfWeek == (int)localNow.DayOfWeek);

        if (row == null || row.IsClosed) return false;
        if (!TryParseTime(row.OpenTime, out var open)) return false;
        if (!TryParseTime(row.CloseTime, out var close)) return false;

        var now = localNow.TimeOfDay;
        return now >= open && now < close;
    }

    private static DateTime ResolveNow(string timeZoneId, out bool resolved)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            resolved = true;
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Loud on purpose: falling back to server local time silently reports the
            // shop open for hours after closing whenever the host runs UTC.
            // Alpine images ship no tzdata — the Dockerfile installs it.
            Console.Error.WriteLine(
                $"[store-hours] Timezone '{timeZoneId}' could not be resolved ({ex.GetType().Name}). " +
                "Falling back to server local time — open/closed WILL be wrong unless the host runs in that zone.");
            resolved = false;
            return DateTime.Now;
        }
    }

    private static string FormatTime(TimeSpan value) => $"{(int)value.TotalHours:D2}:{value.Minutes:D2}";

    private static bool TryParseTime(string? raw, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return TimeSpan.TryParseExact(raw.Trim(), new[] { @"hh\:mm", @"hh\:mm\:ss" },
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    // Cached shape. The other backends read and write this SAME Redis key, so the
    // JSON field names must stay snake_case to match what they emit.
    private class StoreHourRow
    {
        [JsonPropertyName("day_of_week")] public int DayOfWeek { get; set; }
        [JsonPropertyName("open_time")] public string OpenTime { get; set; } = "00:00";
        [JsonPropertyName("close_time")] public string CloseTime { get; set; } = "00:00";
        [JsonPropertyName("is_closed")] public bool IsClosed { get; set; }
    }
}
