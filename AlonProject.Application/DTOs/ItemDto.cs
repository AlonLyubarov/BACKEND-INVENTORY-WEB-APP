namespace AlonProject.Application.DTOs;

public class ItemDto
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }  // SECURITY: Indicates which warehouse owns this item
    public int ProductCatalogId { get; set; }
    public string Location { get; set; } = null!;
    public int Quantity { get; set; }
    public int MinimumStockLevel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ProductCatalogDto? ProductCatalog { get; set; }

    /// <summary>
    /// Computed stock status based on Quantity and MinimumStockLevel.
    /// Not persisted to database - calculated during mapping.
    /// </summary>
    public string Status { get; set; } = "In Stock";
}
