namespace AlonProject.Application.Interfaces;

/// <summary>
/// Authentication service contract for user registration, login, and JWT generation.
/// Handles all authentication-related business logic.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new user account.
    /// Validates username uniqueness, hashes password, creates User entity.
    /// </summary>
    /// <param name="dto">Registration details (username, email, password, role, warehouse)</param>
    /// <returns>UserDto with user information (no password hash)</returns>
    /// <exception cref="InvalidOperationException">Username already exists</exception>
    Task<DTOs.UserDto> RegisterAsync(DTOs.RegisterUserDto dto);

    /// <summary>
    /// Invites (creates) a new Employee/ShiftManager user into a main warehouse.
    /// Called by the warehouse owner. Role is restricted to non-Admin roles.
    /// </summary>
    /// <param name="dto">Invitation details (username, email, password, role)</param>
    /// <param name="warehouseId">The main warehouse the user will be assigned to</param>
    /// <returns>UserDto of the created user</returns>
    /// <exception cref="InvalidOperationException">Username already exists or role is Admin</exception>
    Task<DTOs.UserDto> InviteUserAsync(DTOs.InviteUserDto dto, int warehouseId);

    /// <summary>
    /// Authenticates a user and generates a JWT token.
    /// Verifies credentials and returns Bearer token with 1-hour expiration.
    /// </summary>
    /// <param name="dto">Login credentials (username, password)</param>
    /// <returns>AuthResponseDto with JWT token and user info</returns>
    /// <exception cref="UnauthorizedAccessException">Invalid username or password</exception>
    Task<DTOs.AuthResponseDto> LoginAsync(DTOs.LoginDto dto);

    /// <summary>
    /// Generates a new JWT token for an existing user.
    /// Used after role changes or warehouse assignments to refresh claims in the token.
    /// SECURITY: This prevents stale claims from blocking user access to newly assigned resources.
    /// </summary>
    /// <param name="user">User entity with updated Role/WarehouseId</param>
    /// <returns>JWT token string (not wrapped in AuthResponseDto)</returns>
    Task<string> GenerateJwtTokenAsync(Domain.Entities.User user);
}
