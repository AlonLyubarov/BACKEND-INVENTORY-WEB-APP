namespace AlonProject.Domain.Entities;

public class ProductCatalog
{
    public int Id { get; set; }
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public string? Barcode { get; set; }

    /// <summary>
    /// The Admin (warehouse owner) whose catalog this product belongs to.
    /// Catalogs are tenant-scoped: each owner sees only their own products.
    /// Null only on legacy rows created before scoping was introduced.
    /// </summary>
    public int? OwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User? Owner { get; set; }
    public ICollection<Item> Items { get; set; } = new List<Item>();
}
