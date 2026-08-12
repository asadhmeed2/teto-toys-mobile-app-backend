using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TetoToysMobile.Application.Services;

namespace TetoToysMobile.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
