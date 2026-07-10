using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

/// <summary>
/// DTO for user registration requests.
/// Registration creates a warehouse OWNER: the new user becomes Admin and a main
/// warehouse (WarehouseName/WarehouseLocation) is created atomically with the account.
/// 
/// SECURITY: Do NOT include Role in the request — the server decides.
/// Registration always creates an Admin owner; employees are added later via the
/// owner's invitation endpoint (POST api/warehouse/{id}/invite).
/// </summary>
public class RegisterUserDto
{
    /// <summary>
    /// Unique username for login.
    /// </summary>
    [Required(ErrorMessage = "Username is required")]
    [MaxLength(50, ErrorMessage = "Username must not exceed 50 characters")]
    public string Username { get; set; } = null!;

    /// <summary>
    /// User's email address.
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Must be a valid email address")]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Plain text password (transmitted over HTTPS only).
    /// Backend will hash with BCrypt before storage.
    /// Minimum length enforced by server validation.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    [RegularExpression(@"^(?=.*[A-Z]).*$", ErrorMessage = "Password must contain at least one uppercase letter")]
    public string Password { get; set; } = null!;

    /// <summary>
    /// Name of the main warehouse created for this new owner at registration.
    /// The registering user becomes the Admin owner of this warehouse.
    /// </summary>
    [Required(ErrorMessage = "Warehouse name is required")]
    [MaxLength(100, ErrorMessage = "Warehouse name must not exceed 100 characters")]
    public string WarehouseName { get; set; } = null!;

    /// <summary>
    /// Physical location of the main warehouse created at registration.
    /// </summary>
    [Required(ErrorMessage = "Warehouse location is required")]
    [MaxLength(200, ErrorMessage = "Warehouse location must not exceed 200 characters")]
    public string WarehouseLocation { get; set; } = null!;

    /// <summary>
    /// Real map coordinates of the main warehouse — required so main
    /// warehouses can be navigated between (route planning).
    /// </summary>
    [Required(ErrorMessage = "Warehouse map location (latitude) is required")]
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
    public double? WarehouseLatitude { get; set; }

    [Required(ErrorMessage = "Warehouse map location (longitude) is required")]
    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
    public double? WarehouseLongitude { get; set; }
}
