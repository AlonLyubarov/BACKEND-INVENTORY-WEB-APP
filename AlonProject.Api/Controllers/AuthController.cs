using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AlonProject.Api.Controllers;

/// <summary>
/// Authentication endpoints for user registration and login.
/// Handles JWT token generation and user account management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new warehouse OWNER account.
    /// Creates the user (Role = Admin) and their MAIN warehouse atomically in one transaction.
    /// SECURITY: Role is decided by the server — clients cannot influence it.
    /// Employees are NOT created here; they are invited later by the owner
    /// via POST /api/warehouse/{id}/invite.
    /// </summary>
    /// <param name="dto">Registration details (username, email, password, warehouseName, warehouseLocation)</param>
    /// <returns>Created owner information including the new main warehouse ID/name</returns>
    /// <response code="201">Owner and main warehouse successfully created</response>
    /// <response code="400">Invalid registration data or username already taken</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterUserDto dto)
    {
        _logger.LogInformation("API Request: POST /api/auth/register - Username: {Username}, Email: {Email}, Warehouse: {WarehouseName}",
            dto.Username, dto.Email, dto.WarehouseName);

        try
        {
            var user = await _authService.RegisterAsync(dto);
            _logger.LogInformation("API Response: Owner registered successfully - ID: {UserId}, Main warehouse: {WarehouseId}", user.Id, user.WarehouseId);
            return CreatedAtAction(nameof(Register), new { userId = user.Id }, user);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("API Error: Registration failed - {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Error: Unexpected error during registration");
            return BadRequest(new { error = "An error occurred during registration." });
        }
    }

    /// <summary>
    /// Authenticate user and generate JWT Bearer token.
    /// </summary>
    /// <param name="dto">Login credentials (username, password)</param>
    /// <returns>JWT token with user information and expiration time</returns>
    /// <response code="200">Authentication successful, JWT token returned</response>
    /// <response code="401">Invalid credentials</response>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        _logger.LogInformation("API Request: POST /api/auth/login - Username: {Username}", dto.Username);

        try
        {
            var response = await _authService.LoginAsync(dto);
            _logger.LogInformation("API Response: User authenticated successfully - Username: {Username}, Role: {Role}",
                response.Username, response.Role);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("API Error: Login failed - Invalid credentials for username attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Error: Unexpected error during authentication");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred." });
        }
    }
}
