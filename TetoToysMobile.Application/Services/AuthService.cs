using System.Text.RegularExpressions;
using TetoToysMobile.Application.DTOs;
using TetoToysMobile.Domain.Configuration;
using TetoToysMobile.Domain.Interfaces;

namespace TetoToysMobile.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IRedisCacheService _cache;
    private readonly JwtOptions _jwt;

    private const int MinPasswordLength = 8;
    private const string TermsVersion = "1.0";

    public AuthService(
        IUserRepository users,
        IPasswordHasher hasher,
        ITokenService tokens,
        IRedisCacheService cache,
        JwtOptions jwt)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
        _cache = cache;
        _jwt = jwt;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, string secret)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return AuthResult.Fail("invalid_request", "All fields are required.", 400);

        if (!Regex.IsMatch(request.Email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            return AuthResult.Fail("invalid_request", "Please enter a valid email address.", 400);

        if (request.Password.Length < MinPasswordLength)
            return AuthResult.Fail("invalid_request", $"Password must be at least {MinPasswordLength} characters.", 400);

        if (!request.IsAdult)
            return AuthResult.Fail("invalid_request", "You must confirm that you are 18 years or older.", 400);

        if (!request.TermsAccepted)
            return AuthResult.Fail("invalid_request", "You must accept the Terms of Service and Privacy Policy.", 400);

        if (await _users.GetByEmailAsync(request.Email) != null)
            return AuthResult.Fail("conflict", "An account with this email already exists.", 409);

        var userId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        await _users.CreateUserAsync(
            userId, request.Email.Trim(), _hasher.HashPassword(request.Password),
            request.FirstName.Trim(), request.LastName.Trim(), true,
            now, TermsVersion, request.MarketingOptIn, now);

        var user = await _users.GetByIdAsync(userId);
        if (user == null)
            return AuthResult.Fail("server_error", "Account was created but could not be loaded.", 500);

        return AuthResult.Ok(await IssueTokensAsync(user, secret));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string secret)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return AuthResult.Fail("invalid_request", "Email and password are required.", 400);

        var user = await _users.GetByEmailAsync(request.Email);

        // Same message whether the account is missing, inactive or the password is
        // wrong — anything more specific is an account-enumeration oracle.
        if (user == null || !user.IsActive ||
            !_hasher.VerifyPassword(request.Password, user.PasswordHash))
            return AuthResult.Fail("invalid_grant", "Invalid email or password.", 401);

        await _users.UpdateLastLoginAsync(user.UserId);
        return AuthResult.Ok(await IssueTokensAsync(user, secret));
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string secret)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || !await _cache.ValidateRefreshTokenAsync(refreshToken))
            return AuthResult.Fail("invalid_token", "Missing or invalid refresh token.", 401);

        var userId = _tokens.GetUserIdFromToken(refreshToken);
        if (string.IsNullOrEmpty(userId))
            return AuthResult.Fail("invalid_token", "Malformed refresh token.", 401);

        // Re-read the user so a deactivated account cannot keep minting access tokens
        // for the remaining lifetime of an already-issued refresh token.
        var user = await _users.GetByIdAsync(userId);
        if (user == null || !user.IsActive)
        {
            await _cache.InvalidateRefreshTokenAsync(refreshToken);
            return AuthResult.Fail("invalid_grant", "Account is no longer active.", 401);
        }

        // Rotation: the presented token is burned and a fresh one issued. If a stolen
        // token is replayed after the real client has refreshed, it is already gone.
        await _cache.InvalidateRefreshTokenAsync(refreshToken);

        return AuthResult.Ok(await IssueTokensAsync(user, secret));
    }

    public async Task LogoutAsync(string? refreshToken)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _cache.InvalidateRefreshTokenAsync(refreshToken);
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(Domain.Entities.User user, string secret)
    {
        var accessToken = _tokens.GenerateAccessToken(user.UserId, secret, _jwt.AccessTokenMinutes);
        var refreshToken = _tokens.GenerateRefreshToken(user.UserId, secret, _jwt.RefreshTokenMinutes);

        await _cache.SetRefreshTokenAsync(refreshToken, _jwt.RefreshTokenTtl);

        return new AuthResponse(
            accessToken,
            refreshToken,
            "Bearer",
            _jwt.AccessTokenSeconds,
            new UserDto(user.UserId, user.Email, user.FirstName, user.LastName));
    }
}
