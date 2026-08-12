namespace TetoToysMobile.Domain.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(string userId, string secretKey, int expireMinutes);

    /// <summary>
    /// Refresh tokens carry a random jti so two tokens minted for the same user in the
    /// same second are still distinct — they are used as Redis keys, and a collision
    /// would let one device's logout revoke another's session.
    /// </summary>
    string GenerateRefreshToken(string userId, string secretKey, int expireMinutes);

    string? GetUserIdFromToken(string token);

    /// <summary>Full validation: signature, issuer, audience and lifetime.</summary>
    UserTokenInfo? ValidateAccessToken(string token, string secretKey);
}

public record UserTokenInfo(string UserId);
