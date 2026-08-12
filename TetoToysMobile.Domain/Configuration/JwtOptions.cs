namespace TetoToysMobile.Domain.Configuration;

/// <summary>
/// Token lifetimes, bound from the "Jwt" configuration section.
///
/// Refresh lifetime is much longer than the web apps' because a native client
/// stores its refresh token in the Keychain/Keystore and users expect to stay
/// signed in between sessions rather than re-authenticating each visit.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public TimeSpan AccessTokenTtl { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenTtl { get; set; } = TimeSpan.FromDays(30);

    public int AccessTokenMinutes => (int)AccessTokenTtl.TotalMinutes;
    public int RefreshTokenMinutes => (int)RefreshTokenTtl.TotalMinutes;

    /// <summary>Value for the OAuth-style "expires_in" response field (seconds).</summary>
    public int AccessTokenSeconds => (int)AccessTokenTtl.TotalSeconds;
}
