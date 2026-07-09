using AlonProject.Domain.Enums;

namespace AlonProject.Application.DTOs;

/// <summary>
/// DTO representing a user in responses.
/// This is what the API returns to the frontend - NOT including PasswordHash.
/// </summary>
public class UserDto
{
    /// <summary>
    /// User's unique ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// User's username.
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// User's email address.
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// User's role for authorization.
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// ID of the warehouse this user is assigned to (null if not yet assigned).
    /// </summary>
    public int? WarehouseId { get; set; }

    /// <summary>
    /// Name of the warehouse (denormalized for frontend convenience).
    /// </summary>
    public string? WarehouseName { get; set; }
}
