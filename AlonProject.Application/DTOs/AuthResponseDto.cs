namespace AlonProject.Application.DTOs;

/// <summary>
/// DTO returned from successful login/registration.
/// Contains the JWT token and user information.
/// Frontend stores the token to use in subsequent API calls.
/// </summary>
public class AuthResponseDto
{
    /// <summary>
    /// JWT Bearer token to include in Authorization header for API calls.
    /// Format: Bearer {token}
    /// </summary>
    public string Token { get; set; } = null!;

    /// <summary>
    /// Username of authenticated user (for display).
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// User's role as string (for display/authorization checks in frontend).
    /// </summary>
    public string Role { get; set; } = null!;

    /// <summary>
    /// Token expiration time (UTC).
    /// After this time, token is invalid and user must login again.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Authenticated user's unique ID.
    /// Frontend can use this to identify the user without decoding the JWT.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Warehouse ID assigned to the user, if any.
    /// Null if user has no warehouse assigned yet.
    /// Frontend can use this to determine if user is fully provisioned.
    /// </summary>
    public int? WarehouseId { get; set; }
}
