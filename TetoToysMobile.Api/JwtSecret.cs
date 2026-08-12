/// <summary>
/// Single place that reads the signing key.
///
/// Deliberately has no hard-coded fallback: silently signing with a default key that
/// lives in the repository turns a missing environment variable into a full
/// authentication bypass. A misconfigured service should refuse to serve instead.
/// </summary>
public static class JwtSecret
{
    public static string? Read(IConfiguration config) => config["JWT:SECRET"];

    public static IResult MissingSecretResult()
    {
        Console.Error.WriteLine(
            "[startup] JWT:SECRET is not configured. Set it via environment or appsettings; " +
            "refusing to issue or validate tokens without it.");

        return Results.Json(new
        {
            error = "server_error",
            error_description = "Server configuration error.",
        }, statusCode: 500);
    }
}
