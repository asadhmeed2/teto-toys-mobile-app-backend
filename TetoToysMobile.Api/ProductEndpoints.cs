using TetoToysMobile.Domain.Interfaces;

public static class ProductEndpoints
{
    /// <summary>Matches TatoToys.Api so paging behaves identically for a shared client.</summary>
    private const int DefaultPageSize = 10;

    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        // GET /api/products — public catalogue, displayed products only.
        // Query contract matches TatoToys.Api so an existing client can point here
        // unchanged: ?page&pageSize&search&category&lang, category accepting "All".
        group.MapGet("/products", async (
            HttpContext context, int? page, int? pageSize, string? search, string? category, string? lang) =>
        {
            var pageVal = page is null or < 1 ? 1 : page.Value;
            var pageSizeVal = pageSize is null or < 1 or > 100 ? DefaultPageSize : pageSize.Value;
            var language = string.IsNullOrEmpty(lang) ? "en" : lang;

            // "All" (or anything non-numeric) means no category filter.
            int? categoryId = int.TryParse(category, out var parsed) ? parsed : null;

            var repo = context.RequestServices.GetRequiredService<IProductRepository>();
            var (items, totalCount) = await repo.GetProductsPaginatedAsync(
                pageVal, pageSizeVal, search, categoryId, language);

            return Results.Ok(new
            {
                items = items.Select(Serialize),
                total_count = totalCount,
                page = pageVal,
                page_size = pageSizeVal,
                total_pages = (int)Math.Ceiling((double)totalCount / pageSizeVal),
            });
        });

        // GET /api/products/{productId}
        group.MapGet("/products/{productId}", async (string productId, HttpContext context, string? lang) =>
        {
            var language = string.IsNullOrEmpty(lang) ? "en" : lang;

            var repo = context.RequestServices.GetRequiredService<IProductRepository>();
            var product = await repo.GetProductByIdAsync(productId, language);

            return product == null
                ? Results.NotFound(new { error = "not_found", error_description = "Product not found." })
                : Results.Ok(Serialize(product));
        });

        // GET /api/categories — only categories with active products.
        group.MapGet("/categories", async (HttpContext context, string? lang) =>
        {
            var language = string.IsNullOrEmpty(lang) ? "en" : lang;

            var repo = context.RequestServices.GetRequiredService<IProductRepository>();
            var categories = await repo.GetCategoriesAsync(language);

            return Results.Ok(categories.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                slug = c.Slug,
            }));
        });

        // GET /api/languages — drives the client's language picker.
        group.MapGet("/languages", async (HttpContext context) =>
        {
            var repo = context.RequestServices.GetRequiredService<IProductRepository>();
            var languages = await repo.GetLanguagesAsync();

            return Results.Ok(languages.Select(l => new
            {
                code = l.Code,
                name = l.Name,
                is_rtl = l.IsRtl,
            }));
        });
    }

    internal static object Serialize(TetoToysMobile.Domain.Entities.Product p) => new
    {
        product_id = p.ProductId,
        title = p.Title,
        subtitle = p.Subtitle,
        description = p.Description,
        category = p.Category,
        subcategory = p.Subcategory,
        price = p.Price,
        image_urls = p.ImageUrls,
        part_ids = p.PartIds,
    };
}
