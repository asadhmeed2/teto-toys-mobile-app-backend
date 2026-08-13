namespace TetoToysMobile.Domain.Entities;

public class Product
{
    public string ProductId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public int Category { get; set; }
    public int? Subcategory { get; set; }
    public decimal Price { get; set; }
    public List<string> ImageUrls { get; set; } = new();

    /// <summary>Ids of the parts this product is built from.</summary>
    public List<string> PartIds { get; set; } = new();

    public bool IsDisplayed { get; set; } = true;
}
