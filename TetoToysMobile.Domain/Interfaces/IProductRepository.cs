using TetoToysMobile.Domain.Entities;

namespace TetoToysMobile.Domain.Interfaces;

public interface IProductRepository
{
    /// <summary>Displayed, non-deleted products only — this API is customer-facing.</summary>
    Task<(List<Product> Items, int TotalCount)> GetProductsPaginatedAsync(
        int page, int pageSize, string? search, int? categoryId, string language = "en");

    Task<Product?> GetProductByIdAsync(string productId, string language = "en");

    /// <summary>Categories that currently have at least one active product.</summary>
    Task<List<Category>> GetCategoriesAsync(string language = "en");

    Task<List<SystemLanguage>> GetLanguagesAsync();
}
