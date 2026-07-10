namespace AlonProject.Domain.Entities;

/// <summary>
/// Represents a warehouse/distribution location in the inventory system.
/// Each user belongs to exactly one warehouse.
/// </summary>
public class Warehouse
{
    /// <summary>
    /// Unique identifier for the warehouse.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Warehouse name/code (e.g., "Main Warehouse", "Warehouse-A", "Jerusalem Central").
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Physical location description (e.g., city, address).
    /// </summary>
    public string Location { get; set; } = null!;

    /// <summary>
    /// Real map coordinates of the warehouse. REQUIRED for main warehouses
    /// (used for navigation/route planning between them); sub-warehouses
    /// inherit their parent's coordinates since they share the same site.
    /// Nullable in the schema for backward compatibility with older rows.
    /// </summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>
    /// When this warehouse record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this warehouse record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Owner (Admin) user ID for main warehouses.
    /// FK to User: the Admin who owns and manages this warehouse.
    /// Null for sub-warehouses; ownership is inherited from parent.
    /// SECURITY: Admin A can only manage warehouses where OwnerId = A's UserId (recursively).
    /// </summary>
    public int? OwnerId { get; set; }

    /// <summary>
    /// Parent warehouse ID for sub-warehouses (zones).
    /// Null for main warehouses (root of hierarchy).
    /// Self-referencing FK to Warehouse.
    /// Constraint: ParentWarehouseId, if not null, must reference another Warehouse.
    /// Deleting a warehouse with children fails (DeleteBehavior.Restrict).
    /// </summary>
    public int? ParentWarehouseId { get; set; }

    /// <summary>
    /// Navigation property: Owner (Admin) of this main warehouse.
    /// Null for sub-warehouses.
    /// </summary>
    public User? Owner { get; set; }

    /// <summary>
    /// Navigation property: Parent warehouse (if this is a sub-warehouse).
    /// Null for main warehouses.
    /// </summary>
    public Warehouse? Parent { get; set; }

    /// <summary>
    /// Navigation property: Sub-warehouses (children) of this warehouse (if any).
    /// Empty collection for leaf nodes.
    /// Only main warehouses typically have children.
    /// </summary>
    public ICollection<Warehouse> SubWarehouses { get; set; } = new List<Warehouse>();

    /// <summary>
    /// Navigation property: Users assigned to this warehouse.
    /// </summary>
    public ICollection<User> Users { get; set; } = new List<User>();

    /// <summary>
    /// Navigation property: Items stored in this warehouse.
    /// SECURITY: All items belong to exactly one warehouse for data isolation.
    /// </summary>
    public ICollection<Item> Items { get; set; } = new List<Item>();
}
