using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

/// <summary>
/// Data Transfer Object for Warehouse entity.
/// Used for reading/retrieving warehouse details (GET responses).
/// For creating warehouses, use CreateWarehouseDto instead.
/// </summary>
public class WarehouseDto
{
    /// <summary>
    /// Unique identifier for the warehouse.
    /// Auto-generated on creation.
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
    /// When this warehouse record was created.
    /// Server-calculated, sent to frontend in responses.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this warehouse record was last updated.
    /// Server-calculated, sent to frontend in responses.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Number of users assigned to this warehouse.
    /// Server-calculated, sent to frontend in responses.
    /// </summary>
    public int UserCount { get; set; }

    /// <summary>
    /// Real map coordinates. Set for main warehouses (required at creation);
    /// sub-warehouses carry their parent's coordinates. Null on legacy rows.
    /// </summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>
    /// Parent warehouse ID. Null for main warehouses, set for sub-warehouses.
    /// </summary>
    public int? ParentWarehouseId { get; set; }

    /// <summary>
    /// Sub-warehouses (zones) of this warehouse.
    /// Populated for main warehouses; empty for sub-warehouses.
    /// </summary>
    public List<SubWarehouseDto> SubWarehouses { get; set; } = new();
}

/// <summary>
/// Minimal DTO for sub-warehouse entries nested inside WarehouseDto.
/// </summary>
public class SubWarehouseDto
{
    /// <summary>
    /// Unique identifier of the sub-warehouse.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Sub-warehouse name/zone code.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Physical location description of the sub-warehouse.
    /// </summary>
    public string Location { get; set; } = null!;
}
