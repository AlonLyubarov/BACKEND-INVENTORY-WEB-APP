namespace AlonProject.Domain.Entities;

public class Item
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }  // SECURITY: FK to enforce warehouse isolation
    public int ProductCatalogId { get; set; }
    public string Location { get; set; } = null!;
    public int Quantity { get; set; }
    public int MinimumStockLevel { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // SECURITY: Soft delete flag to preserve audit trail when items are removed from inventory
    // When IsDeleted=true, item should not appear in normal queries but transactions remain queryable for audit
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public Warehouse Warehouse { get; set; } = null!;
    public ProductCatalog ProductCatalog { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
