using TetoToysMobile.Domain.Interfaces;

namespace TetoToysMobile.Infrastructure.Security;

public class PasswordHasher : IPasswordHasher
{
    // Work factor 12 matches the other services; raising it here would make hashes
    // that the web backends can still verify, but not vice versa.
    private const int WorkFactor = 12;

    public string HashPassword(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool VerifyPassword(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}
