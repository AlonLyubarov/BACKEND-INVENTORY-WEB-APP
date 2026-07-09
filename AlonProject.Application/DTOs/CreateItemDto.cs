using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

public class CreateItemDto
{
    /// <summary>
    /// Reference to the product catalog entry.
    /// </summary>
    [Required(ErrorMessage = "ProductCatalogId is required")]
    public int ProductCatalogId { get; set; }

    /// <summary>
    /// Physical location/warehouse where item is stored.
    /// Example: "Shelf A-12", "Zone B3", etc.
    /// </summary>
    [Required(ErrorMessage = "Location is required")]
    [MaxLength(100, ErrorMessage = "Location cannot exceed 100 characters")]
    public string Location { get; set; } = null!;

    /// <summary>
    /// Current quantity in stock - must be non-negative.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be a non-negative number")]
    public int Quantity { get; set; }

    /// <summary>
    /// Minimum stock level threshold for low stock alerts.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "MinimumStockLevel must be a non-negative number")]
    public int MinimumStockLevel { get; set; } = 0;

    /// <summary>
    /// HIERARCHY: Optional destination warehouse node (main or sub-warehouse).
    /// If omitted, the item is created in the caller's assigned warehouse (from JWT claim).
    /// SECURITY: The server verifies (DB-checked) that the caller can access this warehouse
    /// before creating the item — clients cannot place items in warehouses they don't control.
    /// </summary>
    public int? TargetWarehouseId { get; set; }
}
