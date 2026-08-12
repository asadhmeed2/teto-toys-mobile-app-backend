using TetoToysMobile.Application.DTOs;

namespace TetoToysMobile.Application.Services;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, string secret);
    Task<AuthResult> LoginAsync(LoginRequest request, string secret);
    Task<AuthResult> RefreshAsync(string refreshToken, string secret);
    Task LogoutAsync(string? refreshToken);
}
