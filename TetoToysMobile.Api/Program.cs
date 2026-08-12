using DotNetEnv;
using Microsoft.Extensions.Options;
using TetoToysMobile.Application;
using TetoToysMobile.Domain.Configuration;
using TetoToysMobile.Infrastructure;

// Load .env before building the host. clobberExistingVars: false — real Docker or
// system environment variables always win over the file.
Env.Load(options: new LoadOptions(setEnvVars: true, clobberExistingVars: false));

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// CORS matters far less here than for the web apps — native clients aren't bound by
// it — but it's needed for a browser-based dev client or a WebView shell.
var allowedOrigins = (builder.Configuration["CorsOrigins"] ?? "http://localhost:4200")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMobileClients", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
        // No AllowCredentials: this API authenticates with bearer tokens, not
        // cookies, so credentialed cross-origin requests are never needed.
    });
});

// Token lifetimes, plus the bound value exposed directly for layers that don't
// reference Microsoft.Extensions.Options.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtOptions>>().Value);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Fail loudly at boot rather than per-request: a service that can't verify tokens
// should be obvious immediately, not after someone tries to log in.
if (string.IsNullOrEmpty(app.Configuration["JWT:SECRET"]))
{
    app.Logger.LogCritical(
        "JWT:SECRET is not configured. Set it in the environment or appsettings before starting.");
}

// First in the pipeline so even short-circuited responses (429s) carry the headers.
app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors("AllowMobileClients");

// After CORS so 429 responses still carry CORS headers and a browser-based client
// can read them instead of reporting an opaque network error.
app.UseRedisRateLimiting();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapAuthEndpoints();
app.MapProductEndpoints();
app.MapFavoritesEndpoints();
app.MapStoreHoursEndpoints();

// Container/orchestrator liveness probe.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
