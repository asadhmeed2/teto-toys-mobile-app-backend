using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TetoToysMobile.Domain.Interfaces;
using TetoToysMobile.Infrastructure.Caching;
using TetoToysMobile.Infrastructure.Data;
using TetoToysMobile.Infrastructure.Security;

namespace TetoToysMobile.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Redis ───────────────────────────────────────────────────────────────
        var redisHost = configuration["Redis:Host"] ?? "127.0.0.1";
        var redisPort = configuration["Redis:Port"] ?? "6379";
        var redisPassword = configuration["Redis:Password"];

        var redisConfig = new ConfigurationOptions
        {
            EndPoints = { $"{redisHost}:{redisPort}" },
            Password = string.IsNullOrEmpty(redisPassword) ? null : redisPassword,
            ConnectTimeout = 5000,
            SyncTimeout = 3000,
            // Don't crash the app on boot if Redis is briefly unavailable; the
            // multiplexer reconnects and callers fail open where it matters.
            AbortOnConnectFail = false,
        };

        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConfig));
        services.AddScoped<IRedisCacheService, RedisCacheService>();

        // ── MySQL ───────────────────────────────────────────────────────────────
        var rawConnectionString = configuration["MySQL:ConnectionString"]
            ?? configuration.GetConnectionString("DefaultConnection");

        var builder = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(rawConnectionString);

        // Individual MySQL:* settings override pieces of the connection string, which
        // is how docker-compose points the container at the shared_mysql host.
        if (!string.IsNullOrEmpty(configuration["MySQL:Server"])) builder.Server = configuration["MySQL:Server"];
        if (!string.IsNullOrEmpty(configuration["MySQL:Port"]) && uint.TryParse(configuration["MySQL:Port"], out var port)) builder.Port = port;
        if (!string.IsNullOrEmpty(configuration["MySQL:Database"])) builder.Database = configuration["MySQL:Database"];
        if (!string.IsNullOrEmpty(configuration["MySQL:User"])) builder.UserID = configuration["MySQL:User"];
        if (!string.IsNullOrEmpty(configuration["MySQL:Password"])) builder.Password = configuration["MySQL:Password"];

        var connectionString = builder.ConnectionString;

        services.AddScoped<IUserRepository>(_ => new UserRepository(connectionString));
        services.AddScoped<IProductRepository>(_ => new ProductRepository(connectionString));
        services.AddScoped<IFavoritesRepository>(_ => new FavoritesRepository(connectionString));
        services.AddScoped<IStoreHoursRepository>(_ => new StoreHoursRepository(connectionString));

        // ── Security ────────────────────────────────────────────────────────────
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }
}
