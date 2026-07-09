using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

public class CreateProductCatalogDto
{
    /// <summary>
    /// Product SKU - unique identifier for the product.
    /// </summary>
    [Required(ErrorMessage = "SKU is required")]
    [MaxLength(50, ErrorMessage = "SKU cannot exceed 50 characters")]
    public string Sku { get; set; } = null!;

    /// <summary>
    /// Product name/description.
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Product price - must be non-negative.
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Price must be a non-negative number")]
    public decimal Price { get; set; }

    /// <summary>
    /// Product barcode - optional.
    /// </summary>
    [MaxLength(100, ErrorMessage = "Barcode cannot exceed 100 characters")]
    public string? Barcode { get; set; }
}
