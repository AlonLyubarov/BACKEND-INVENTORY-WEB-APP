using System.ComponentModel.DataAnnotations;
using AlonProject.Domain.Enums;

namespace AlonProject.Application.DTOs;

/// <summary>
/// DTO for inviting a user (Employee/ShiftManager) into a main warehouse.
/// Used by the warehouse owner via POST api/warehouse/{warehouseId}/invite.
/// SECURITY: Role is restricted to Employee or ShiftManager — Admin is rejected (400).
/// The created user's WarehouseId is set to the target main warehouse.
/// </summary>
public class InviteUserDto
{
    /// <summary>
    /// Unique username for the invited user's login.
    /// </summary>
    [Required(ErrorMessage = "Username is required")]
    [MaxLength(50, ErrorMessage = "Username must not exceed 50 characters")]
    public string Username { get; set; } = null!;

    /// <summary>
    /// Invited user's email address.
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Must be a valid email address")]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Initial password for the invited user (hashed with BCrypt before storage).
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    [RegularExpression(@"^(?=.*[A-Z]).*$", ErrorMessage = "Password must contain at least one uppercase letter")]
    public string Password { get; set; } = null!;

    /// <summary>
    /// Role for the invited user. Only Employee or ShiftManager are accepted.
    /// Admin is rejected with 400 — ownership cannot be granted via invitation.
    /// </summary>
    [Required(ErrorMessage = "Role is required")]
    public UserRole Role { get; set; }
}
