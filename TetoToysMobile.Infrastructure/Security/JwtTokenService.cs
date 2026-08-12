using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TetoToysMobile.Domain.Interfaces;

namespace TetoToysMobile.Infrastructure.Security;

public class JwtTokenService : ITokenService
{
    public const string Issuer = "tatotoys-api";
    public const string Audience = "tatotoys-mobile";

    public string GenerateAccessToken(string userId, string secretKey, int expireMinutes) =>
        GenerateToken(userId, secretKey, expireMinutes, tokenType: "access");

    public string GenerateRefreshToken(string userId, string secretKey, int expireMinutes) =>
        GenerateToken(userId, secretKey, expireMinutes, tokenType: "refresh");

    private static string GenerateToken(string userId, string secretKey, int expireMinutes, string tokenType)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(secretKey);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new("token_type", tokenType),
            // Refresh tokens are used verbatim as Redis keys. Without a unique jti two
            // tokens minted for the same user in the same second would be identical,
            // so logging out one device would silently revoke another.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expireMinutes),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
        };

        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    public string? GetUserIdFromToken(string token)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            // ReadJwtToken applies no inbound claim mapping, so "sub" stays "sub".
            return jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        }
        catch
        {
            return null;
        }
    }

    public UserTokenInfo? ValidateAccessToken(string token, string secretKey)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secretKey);

            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            }, out var validated);

            var jwt = (JwtSecurityToken)validated;

            // A refresh token is signed with the same key, so without this check it
            // would be accepted as a bearer credential on protected endpoints.
            if (jwt.Claims.FirstOrDefault(c => c.Type == "token_type")?.Value != "access")
                return null;

            var userId = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            return string.IsNullOrEmpty(userId) ? null : new UserTokenInfo(userId);
        }
        catch
        {
            return null;
        }
    }
}
