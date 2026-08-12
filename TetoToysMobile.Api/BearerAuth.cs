using TetoToysMobile.Domain.Interfaces;

/// <summary>
/// Resolves the caller from the Authorization header.
///
/// Mobile clients send "Authorization: Bearer &lt;access token&gt;" — there is no cookie
/// and no server-side session lookup, so the token's signature is the only thing
/// standing between a request and someone else's account. It is fully validated
/// (signature, issuer, audience, lifetime) on every call.
/// </summary>
public static class BearerAuth
{
    private const string Scheme = "Bearer ";

    public static (bool Authorized, string? UserId, IResult? ErrorResult) Authenticate(
        HttpContext context, string secret)
    {
        var header = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(header) || !header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return (false, null, Results.Json(new
            {
                error = "unauthorized",
                error_description = "Missing or invalid Authorization header.",
            }, statusCode: 401));
        }

        var tokenService = context.RequestServices.GetRequiredService<ITokenService>();
        var info = tokenService.ValidateAccessToken(header[Scheme.Length..].Trim(), secret);

        if (info == null)
        {
            return (false, null, Results.Json(new
            {
                error = "unauthorized",
                error_description = "Token is invalid or expired.",
            }, statusCode: 401));
        }

        return (true, info.UserId, null);
    }
}
