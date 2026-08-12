using MySql.Data.MySqlClient;
using TetoToysMobile.Domain.Entities;
using TetoToysMobile.Domain.Interfaces;

namespace TetoToysMobile.Infrastructure.Data;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private const string SelectColumns =
        "user_id, email, password_hash, first_name, last_name, is_active";

    public async Task<User?> GetByEmailAsync(string email) =>
        await QuerySingleAsync($"SELECT {SelectColumns} FROM users WHERE email = @value", email);

    public async Task<User?> GetByIdAsync(string userId) =>
        await QuerySingleAsync($"SELECT {SelectColumns} FROM users WHERE user_id = @value", userId);

    private async Task<User?> QuerySingleAsync(string sql, string value)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.Add("@value", MySqlDbType.VarChar).Value = value;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var activeOrdinal = reader.GetOrdinal("is_active");
        return new User
        {
            UserId = reader.GetIdString("user_id"),
            Email = reader.GetStringOrEmpty("email"),
            PasswordHash = reader.GetStringOrEmpty("password_hash"),
            FirstName = reader.GetStringOrEmpty("first_name"),
            LastName = reader.GetStringOrEmpty("last_name"),
            IsActive = !reader.IsDBNull(activeOrdinal) && reader.GetBoolean(activeOrdinal),
        };
    }

    public async Task CreateUserAsync(
        string userId, string email, string passwordHash,
        string firstName, string lastName, bool isAdult,
        DateTime termsAcceptedAt, string termsVersion,
        bool marketingOptIn, DateTime createdAt)
    {
        const string sql = @"
            INSERT INTO users
                (user_id, email, password_hash, first_name, last_name,
                 is_adult, terms_accepted_at, terms_version, marketing_opt_in, created_at, is_active)
            VALUES
                (@userId, @email, @passwordHash, @firstName, @lastName,
                 @isAdult, @termsAcceptedAt, @termsVersion, @marketingOptIn, @createdAt, 1)";

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
        cmd.Parameters.AddWithValue("@firstName", firstName);
        cmd.Parameters.AddWithValue("@lastName", lastName);
        cmd.Parameters.AddWithValue("@isAdult", isAdult);
        cmd.Parameters.AddWithValue("@termsAcceptedAt", termsAcceptedAt);
        cmd.Parameters.AddWithValue("@termsVersion", termsVersion);
        cmd.Parameters.AddWithValue("@marketingOptIn", marketingOptIn);
        cmd.Parameters.AddWithValue("@createdAt", createdAt);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateLastLoginAsync(string userId)
    {
        const string sql = "UPDATE users SET last_login = @now WHERE user_id = @userId";

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@userId", userId);
        await cmd.ExecuteNonQueryAsync();
    }
}
