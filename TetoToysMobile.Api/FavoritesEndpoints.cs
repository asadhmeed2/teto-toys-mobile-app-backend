using TetoToysMobile.Domain.Interfaces;

public static class FavoritesEndpoints
{
    public static void MapFavoritesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/favorites");

        // GET /api/favorites — the caller's favourites, hydrated.
        group.MapGet("/", async (HttpContext context, string? lang) =>
        {
            var (ok, userId, error) = Authenticate(context);
            if (!ok) return error!;

            var language = string.IsNullOrEmpty(lang) ? "en" : lang;
            var repo = context.RequestServices.GetRequiredService<IFavoritesRepository>();

            var items = await repo.GetFavoritesAsync(userId!, language);
            return Results.Ok(new { items = items.Select(ProductEndpoints.Serialize) });
        });

        // GET /api/favorites/ids — ids only, so a product list can render its hearts
        // without pulling every favourited product.
        group.MapGet("/ids", async (HttpContext context) =>
        {
            var (ok, userId, error) = Authenticate(context);
            if (!ok) return error!;

            var repo = context.RequestServices.GetRequiredService<IFavoritesRepository>();
            return Results.Ok(new { product_ids = await repo.GetFavoriteIdsAsync(userId!) });
        });

        // POST /api/favorites/{productId} — idempotent.
        group.MapPost("/{productId}", async (string productId, HttpContext context) =>
        {
            var (ok, userId, error) = Authenticate(context);
            if (!ok) return error!;

            var repo = context.RequestServices.GetRequiredService<IFavoritesRepository>();

            if (!await repo.ProductExistsAsync(productId))
                return Results.NotFound(new { error = "not_found", error_description = "Product not found." });

            await repo.AddFavoriteAsync(userId!, productId);
            return Results.Ok(new { product_id = productId, is_favorite = true });
        });

        // DELETE /api/favorites/{productId} — idempotent.
        group.MapDelete("/{productId}", async (string productId, HttpContext context) =>
        {
            var (ok, userId, error) = Authenticate(context);
            if (!ok) return error!;

            var repo = context.RequestServices.GetRequiredService<IFavoritesRepository>();
            await repo.RemoveFavoriteAsync(userId!, productId);

            return Results.Ok(new { product_id = productId, is_favorite = false });
        });
    }

    private static (bool Ok, string? UserId, IResult? Error) Authenticate(HttpContext context)
    {
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        var secret = JwtSecret.Read(config);

        if (string.IsNullOrEmpty(secret))
            return (false, null, JwtSecret.MissingSecretResult());

        return BearerAuth.Authenticate(context, secret);
    }
}
