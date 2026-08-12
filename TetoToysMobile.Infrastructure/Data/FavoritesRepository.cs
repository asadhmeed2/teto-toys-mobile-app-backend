using MySql.Data.MySqlClient;
using TetoToysMobile.Domain.Entities;
using TetoToysMobile.Domain.Interfaces;

namespace TetoToysMobile.Infrastructure.Data;

public class FavoritesRepository : IFavoritesRepository
{
    private readonly string _connectionString;

    public FavoritesRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<Product>> GetFavoritesAsync(string userId, string language = "en")
    {
        // Requested language, then English, then any translation — a product created
        // only in Hebrew still shows a title instead of NULL.
        const string sql = @"
            SELECT p.product_id,
                   COALESCE(req.title, fb.title,
                            (SELECT pt.title FROM product_translations pt
                             WHERE pt.product_id = p.product_id ORDER BY pt.language_code LIMIT 1)) AS title,
                   COALESCE(req.subtitle, fb.subtitle) AS subtitle,
                   COALESCE(req.description, fb.description) AS description,
                   p.category, p.subcategory, p.price, p.image_urls
            FROM favorites_products f
            JOIN products p ON p.product_id = f.product_id
            LEFT JOIN product_translations req ON req.product_id = p.product_id AND req.language_code = @language
            LEFT JOIN product_translations fb  ON fb.product_id  = p.product_id AND fb.language_code  = 'en'
            WHERE f.user_id = @userId AND p.is_deleted = 0 AND p.is_displayed = 1
            ORDER BY f.created_at DESC";

        var items = new List<Product>();

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@language", language);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(ProductRepository.MapProduct(reader));
        }

        return items;
    }

    public async Task<List<string>> GetFavoriteIdsAsync(string userId)
    {
        const string sql = "SELECT product_id FROM favorites_products WHERE user_id = @userId";

        var ids = new List<string>();

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetIdString("product_id"));
        }

        return ids;
    }

    public async Task AddFavoriteAsync(string userId, string productId)
    {
        // INSERT IGNORE so favouriting twice is idempotent rather than a 500 on the
        // (user_id, product_id) primary key.
        const string sql = @"
            INSERT IGNORE INTO favorites_products (user_id, product_id)
            VALUES (@userId, @productId)";

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@productId", productId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveFavoriteAsync(string userId, string productId)
    {
        const string sql = "DELETE FROM favorites_products WHERE user_id = @userId AND product_id = @productId";

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@productId", productId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> ProductExistsAsync(string productId)
    {
        const string sql = "SELECT COUNT(1) FROM products WHERE product_id = @productId AND is_deleted = 0";

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@productId", productId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }
}
