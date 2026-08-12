using TetoToysMobile.Domain.Entities;

namespace TetoToysMobile.Domain.Interfaces;

public interface IFavoritesRepository
{
    Task<List<Product>> GetFavoritesAsync(string userId, string language = "en");

    /// <summary>Just the ids — cheap enough for the client to render heart icons in a list.</summary>
    Task<List<string>> GetFavoriteIdsAsync(string userId);

    Task AddFavoriteAsync(string userId, string productId);
    Task RemoveFavoriteAsync(string userId, string productId);
    Task<bool> ProductExistsAsync(string productId);
}
