using MySql.Data.MySqlClient;
using TetoToysMobile.Domain.Entities;
using TetoToysMobile.Domain.Interfaces;

namespace TetoToysMobile.Infrastructure.Data;

public class StoreHoursRepository : IStoreHoursRepository
{
    private readonly string _connectionString;

    public StoreHoursRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<StoreHours>> GetAllAsync()
    {
        const string sql = @"
            SELECT day_of_week, open_time, close_time, is_closed
            FROM store_hours
            ORDER BY day_of_week ASC";

        var result = new List<StoreHours>();

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new StoreHours
            {
                DayOfWeek = reader.GetInt32(reader.GetOrdinal("day_of_week")),
                // GetFieldValue<TimeSpan>, not GetTimeSpan: the reader is typed as the
                // base DbDataReader, which has no provider-specific accessor.
                OpenTime = reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("open_time")),
                CloseTime = reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("close_time")),
                IsClosed = reader.GetBoolean(reader.GetOrdinal("is_closed")),
            });
        }

        return result;
    }
}
