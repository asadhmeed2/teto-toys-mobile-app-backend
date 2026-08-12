using TetoToysMobile.Application.DTOs;
using TetoToysMobile.Application.Services;
using TetoToysMobile.Domain.Interfaces;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        // POST /api/auth/register
        group.MapPost("/register", async (RegisterRequest request, HttpContext context) =>
        {
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var secret = JwtSecret.Read(config);
            if (string.IsNullOrEmpty(secret)) return JwtSecret.MissingSecretResult();

            var auth = context.RequestServices.GetRequiredService<IAuthService>();
            return ToResult(await auth.RegisterAsync(request, secret), 201);
        });

        // POST /api/auth/login
        group.MapPost("/login", async (LoginRequest request, HttpContext context) =>
        {
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var secret = JwtSecret.Read(config);
            if (string.IsNullOrEmpty(secret)) return JwtSecret.MissingSecretResult();

            var auth = context.RequestServices.GetRequiredService<IAuthService>();
            return ToResult(await auth.LoginAsync(request, secret));
        });

        // POST /api/auth/refresh — token in the body, not a cookie.
        // Rotating: the presented token is invalidated and a new pair returned.
        group.MapPost("/refresh", async (RefreshRequest request, HttpContext context) =>
        {
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var secret = JwtSecret.Read(config);
            if (string.IsNullOrEmpty(secret)) return JwtSecret.MissingSecretResult();

            var auth = context.RequestServices.GetRequiredService<IAuthService>();
            return ToResult(await auth.RefreshAsync(request?.RefreshToken ?? string.Empty, secret));
        });

        // POST /api/auth/logout — revokes the supplied refresh token.
        group.MapPost("/logout", async (RefreshRequest request, HttpContext context) =>
        {
            var auth = context.RequestServices.GetRequiredService<IAuthService>();
            await auth.LogoutAsync(request?.RefreshToken);
            return Results.Ok(new { message = "Logged out successfully." });
        });

        // GET /api/auth/me
        group.MapGet("/me", async (HttpContext context) =>
        {
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var secret = JwtSecret.Read(config);
            if (string.IsNullOrEmpty(secret)) return JwtSecret.MissingSecretResult();

            var (authorized, userId, error) = BearerAuth.Authenticate(context, secret);
            if (!authorized) return error!;

            var users = context.RequestServices.GetRequiredService<IUserRepository>();
            var user = await users.GetByIdAsync(userId!);

            if (user == null || !user.IsActive)
                return Results.Json(new { error = "unauthorized", error_description = "Account is no longer active." }, statusCode: 401);

            return Results.Ok(new
            {
                user_id = user.UserId,
                email = user.Email,
                first_name = user.FirstName,
                last_name = user.LastName,
            });
        });
    }

    private static IResult ToResult(AuthResult result, int successStatus = 200)
    {
        if (!result.Success)
        {
            return Results.Json(new
            {
                error = result.Error,
                error_description = result.ErrorDescription,
            }, statusCode: result.StatusCode);
        }

        return Results.Json(result.Response, statusCode: successStatus);
    }
}
