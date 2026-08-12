using System.Text.Json.Serialization;

namespace TetoToysMobile.Application.DTOs;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string LastName,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("is_adult")] bool IsAdult,
    [property: JsonPropertyName("terms_accepted")] bool TermsAccepted,
    [property: JsonPropertyName("marketing_opt_in")] bool MarketingOptIn
);

/// <summary>
/// The refresh token travels in the request body, not a cookie: native clients have
/// no cookie jar and keep it in the Keychain/Keystore instead.
/// </summary>
public record RefreshRequest(
    [property: JsonPropertyName("refresh_token")] string RefreshToken
);

public record AuthResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("user")] UserDto User
);

public record UserDto(
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string LastName
);

/// <summary>Result envelope so endpoints can map failures to status codes without exceptions.</summary>
public record AuthResult(
    bool Success,
    AuthResponse? Response,
    string? Error,
    string? ErrorDescription,
    int StatusCode
)
{
    public static AuthResult Ok(AuthResponse response) => new(true, response, null, null, 200);
    public static AuthResult Fail(string error, string description, int statusCode) =>
        new(false, null, error, description, statusCode);
}
