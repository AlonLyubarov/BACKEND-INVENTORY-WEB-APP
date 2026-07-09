using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

/// <summary>
/// Data Transfer Object for creating a new warehouse.
/// Used only for POST requests to create warehouses.
/// Frontend sends only Name and Location.
/// </summary>
public class CreateWarehouseDto
{
    /// <summary>
    /// Warehouse name/code (e.g., "Main Warehouse", "Warehouse-A", "Jerusalem Central").
    /// Required and should be unique.
    /// </summary>
    [Required(ErrorMessage = "Warehouse name is required")]
    [MaxLength(100, ErrorMessage = "Warehouse name cannot exceed 100 characters")]
    [MinLength(2, ErrorMessage = "Warehouse name must be at least 2 characters")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Physical location description (e.g., city, address).
    /// </summary>
    [Required(ErrorMessage = "Location is required")]
    [MaxLength(200, ErrorMessage = "Location cannot exceed 200 characters")]
    [MinLength(2, ErrorMessage = "Location must be at least 2 characters")]
    public string Location { get; set; } = null!;
}
