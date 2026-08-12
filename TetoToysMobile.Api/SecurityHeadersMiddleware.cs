using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

/// <summary>
/// Baseline response hardening.
///
/// These are cheap and apply to every response. Note this is a JSON API, so the
/// headers that matter here are the ones limiting how a browser may *interpret* and
/// *embed* responses — a full CSP belongs on whatever serves the Angular bundles,
/// not here, since an API response has no scripts to restrict.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Stop the browser second-guessing Content-Type. Without it, a JSON response
        // an attacker can influence may be sniffed as HTML and executed.
        headers["X-Content-Type-Options"] = "nosniff";

        // No reason to ever frame an API response; blocks clickjacking on any
        // HTML error page the stack might emit.
        headers["X-Frame-Options"] = "DENY";

        // Don't spill the full URL (which can carry ids or tokens) to third parties.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // The API needs none of these device APIs; deny them explicitly.
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), interest-cohort=()";

        // Legacy header, but harmless and still honoured by some older browsers.
        headers["X-Permitted-Cross-Domain-Policies"] = "none";

        return _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>Register first so even short-circuited responses (429s, redirects) carry the headers.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
