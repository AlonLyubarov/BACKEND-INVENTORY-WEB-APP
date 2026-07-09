namespace AlonProject.Application.DTOs;

/// <summary>
/// Comprehensive warehouse details DTO.
/// Contains warehouse information along with its items and transaction summary.
/// Used in: GET /api/warehouse/{id}/details
/// </summary>
public class WarehouseDetailsDto
{
    /// <summary>
    /// Warehouse basic information.
    /// </summary>
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Location { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Number of users assigned to this warehouse.
    /// </summary>
    public int UserCount { get; set; }

    /// <summary>
    /// All items stored in this warehouse.
    /// </summary>
    public IEnumerable<ItemDto> Items { get; set; } = new List<ItemDto>();

    /// <summary>
    /// All transactions related to items in this warehouse.
    /// </summary>
    public IEnumerable<TransactionDto> Transactions { get; set; } = new List<TransactionDto>();

    /// <summary>
    /// Warehouse inventory summary statistics.
    /// </summary>
    public InventorySummaryDto? InventorySummary { get; set; }
}

/// <summary>
/// Inventory summary statistics for a warehouse.
/// </summary>
public class InventorySummaryDto
{
    /// <summary>
    /// Total number of unique items in the warehouse.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Total quantity across all items.
    /// </summary>
    public int TotalQuantity { get; set; }

    /// <summary>
    /// Number of items with low stock (below minimum level).
    /// </summary>
    public int LowStockItems { get; set; }

    /// <summary>
    /// Number of out-of-stock items (quantity = 0).
    /// </summary>
    public int OutOfStockItems { get; set; }

    /// <summary>
    /// Total transactions in this warehouse.
    /// </summary>
    public int TransactionCount { get; set; }
}
