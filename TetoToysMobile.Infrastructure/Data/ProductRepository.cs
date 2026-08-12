using System.Data.Common;
using System.Text.Json;
using MySql.Data.MySqlClient;
using TetoToysMobile.Domain.Entities;
using TetoToysMobile.Domain.Interfaces;

namespace TetoToysMobile.Infrastructure.Data;

public class ProductRepository : IProductRepository
{
    private readonly string _connectionString;

    public ProductRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Requested language, then English, then any translation. The third fallback
    /// matters because a product may be authored only in a non-English language,
    /// leaving no 'en' row — without it the title comes back NULL.
    /// </summary>
    private const string TitleExpression = @"
        COALESCE(req.title, fb.title,
                 (SELECT pt.title FROM product_translations pt
                  WHERE pt.product_id = p.product_id ORDER BY pt.language_code LIMIT 1))";

    // Customer-facing: hidden and soft-deleted products must never appear.
    private const string VisibilityFilter = " WHERE p.is_deleted = 0 AND p.is_displayed = 1";

    private const string TranslationJoins = @"
        LEFT JOIN product_translations req ON req.product_id = p.product_id AND req.language_code = @language
        LEFT JOIN product_translations fb  ON fb.product_id  = p.product_id AND fb.language_code  = 'en'";

    public async Task<(List<Product> Items, int TotalCount)> GetProductsPaginatedAsync(
        int page, int pageSize, string? search, int? categoryId, string language = "en")
    {
        var items = new List<Product>();
        var offset = (page - 1) * pageSize;

        var filters = VisibilityFilter;
        if (categoryId.HasValue) filters += " AND p.category = @categoryId";
        if (!string.IsNullOrEmpty(search))
            filters += $" AND ({TitleExpression} LIKE @search OR COALESCE(req.description, fb.description) LIKE @search)";

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();

        var countSql = $"SELECT COUNT(1) FROM products p{TranslationJoins}{filters}";
        int totalCount;
        await using (var countCmd = new MySqlCommand(countSql, conn))
        {
            BindFilters(countCmd, search, categoryId, language);
            totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        }

        var itemsSql = $@"
            SELECT p.product_id,
                   {TitleExpression} AS title,
                   COALESCE(req.subtitle, fb.subtitle) AS subtitle,
                   COALESCE(req.description, fb.description) AS description,
                   p.category, p.subcategory, p.price, p.image_urls
            FROM products p{TranslationJoins}{filters}
            ORDER BY p.created_at DESC
            LIMIT @limit OFFSET @offset";

        await using (var itemsCmd = new MySqlCommand(itemsSql, conn))
        {
            BindFilters(itemsCmd, search, categoryId, language);
            itemsCmd.Parameters.AddWithValue("@limit", pageSize);
            itemsCmd.Parameters.AddWithValue("@offset", offset);

            await using var reader = await itemsCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapProduct(reader));
            }
        }

        return (items, totalCount);
    }

    private static void BindFilters(MySqlCommand cmd, string? search, int? categoryId, string language)
    {
        cmd.Parameters.AddWithValue("@language", language);
        if (categoryId.HasValue) cmd.Parameters.AddWithValue("@categoryId", categoryId.Value);
        if (!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("@search", $"%{search}%");
    }

    public async Task<Product?> GetProductByIdAsync(string productId, string language = "en")
    {
        var sql = $@"
            SELECT p.product_id,
                   {TitleExpression} AS title,
                   COALESCE(req.subtitle, fb.subtitle) AS subtitle,
                   COALESCE(req.description, fb.description) AS description,
                   p.category, p.subcategory, p.price, p.image_urls
            FROM products p{TranslationJoins}
            WHERE p.product_id = @productId AND p.is_deleted = 0 AND p.is_displayed = 1";

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@productId", productId);
        cmd.Parameters.AddWithValue("@language", language);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapProduct(reader) : null;
    }

    public async Task<List<Category>> GetCategoriesAsync(string language = "en")
    {
        const string sql = @"
            SELECT c.id,
                   COALESCE(req.name, fb.name,
                            (SELECT ct.name FROM category_translations ct
                             WHERE ct.category_id = c.id ORDER BY ct.language_code LIMIT 1)) AS name,
                   c.slug, c.number_of_active_products
            FROM categories c
            LEFT JOIN category_translations req ON req.category_id = c.id AND req.language_code = @language
            LEFT JOIN category_translations fb  ON fb.category_id  = c.id AND fb.language_code  = 'en'
            WHERE c.number_of_active_products > 0
            ORDER BY name ASC";

        var items = new List<Category>();

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@language", language);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new Category
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Name = reader.GetStringOrEmpty("name"),
                Slug = reader.GetStringOrEmpty("slug"),
                NumberOfActiveProducts = reader.GetInt32(reader.GetOrdinal("number_of_active_products")),
            });
        }

        return items;
    }

    public async Task<List<SystemLanguage>> GetLanguagesAsync()
    {
        const string sql = "SELECT code, name, is_rtl FROM system_languages ORDER BY code ASC";

        var items = new List<SystemLanguage>();

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new SystemLanguage
            {
                Code = reader.GetStringOrEmpty("code"),
                Name = reader.GetStringOrEmpty("name"),
                IsRtl = reader.GetBoolean(reader.GetOrdinal("is_rtl")),
            });
        }

        return items;
    }

    /// <summary>Shared with FavoritesRepository, which selects the same column set.</summary>
    internal static Product MapProduct(DbDataReader reader)
    {
        var subcategoryOrdinal = reader.GetOrdinal("subcategory");
        var imagesOrdinal = reader.GetOrdinal("image_urls");

        return new Product
        {
            ProductId = reader.GetIdString("product_id"),
            // Guarded: NULL when the row has no translation in any language.
            Title = reader.GetStringOrEmpty("title"),
            Subtitle = reader.GetStringOrNull("subtitle"),
            Description = reader.GetStringOrNull("description"),
            Category = reader.GetInt32(reader.GetOrdinal("category")),
            Subcategory = reader.IsDBNull(subcategoryOrdinal) ? null : reader.GetInt32(subcategoryOrdinal),
            Price = reader.GetDecimal(reader.GetOrdinal("price")),
            ImageUrls = reader.IsDBNull(imagesOrdinal)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(reader.GetString(imagesOrdinal)) ?? new List<string>(),
        };
    }
}
