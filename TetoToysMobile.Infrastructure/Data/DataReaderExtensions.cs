using System.Data.Common;

namespace TetoToysMobile.Infrastructure.Data;

internal static class DataReaderExtensions
{
    /// <summary>
    /// Reads a CHAR(36) id column as a string.
    ///
    /// MySql.Data materialises CHAR(36) as Guid by default, so GetString on such a
    /// column throws InvalidCastException — but the same column comes back as a plain
    /// string when the value isn't GUID-shaped or the connection uses OldGuids.
    /// Reading the boxed value handles both.
    ///
    /// Declared on DbDataReader, not MySqlDataReader: ExecuteReaderAsync returns the
    /// base type, so an extension on the concrete type would never bind.
    /// </summary>
    public static string GetIdString(this DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal)) return string.Empty;

        var value = reader.GetValue(ordinal);
        return value as string ?? value?.ToString() ?? string.Empty;
    }

    public static string GetStringOrEmpty(this DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    public static string? GetStringOrNull(this DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
