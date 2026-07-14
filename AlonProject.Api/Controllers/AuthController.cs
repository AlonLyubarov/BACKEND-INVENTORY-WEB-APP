using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    /// SECURITY: Role is decided by the server � clients cannot influence it.
    /// Employees are NOT created here; they are invited later by the owner
    /// via POST /api/warehouse/{id}/invite.
    /// </summary>
    /// <param name="dto">Registration details (username, email, password, warehouseName, warehouseLocation)</param>
    /// <returns>Created owner information including the new main warehouse ID/name</returns>
    /// <response code="201">Owner and main warehouse successfully created</response>
    /// <response code="400">Invalid registration data or username already taken</response>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
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
            return Created($"/api/users/{user.Id}", user);
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
    [EnableRateLimiting("auth")]
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
            return OkWithRefreshCookie(response);
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

    /// <summary>
    /// POST api/auth/verify-email
    /// Confirms the caller's email using the one-time token from the email link.
    /// </summary>
    /// <response code="200">Email verified — the user can now sign in</response>
    /// <response code="400">Invalid, used, or expired token</response>
    [HttpPost("verify-email")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
    {
        _logger.LogInformation("API Request: POST /api/auth/verify-email");
        try
        {
            await _authService.VerifyEmailAsync(dto.Token);
            return Ok(new { message = "Email verified. You can sign in now." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("API Error: Email verification failed - {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST api/auth/resend-verification
    /// Re-sends the verification email. Always returns 200 so account
    /// existence cannot be probed.
    /// </summary>
    [HttpPost("resend-verification")]
    [EnableRateLimiting("email")]
    public async Task<ActionResult> ResendVerification([FromBody] ResendVerificationDto dto)
    {
        _logger.LogInformation("API Request: POST /api/auth/resend-verification");
        try
        {
            await _authService.ResendVerificationAsync(dto.Email);
        }
        catch (Exception ex)
        {
            // Never leak delivery/account details on this endpoint
            _logger.LogError(ex, "API Error: Resend verification failed");
        }
        return Ok(new { message = "If that email belongs to an unverified account, a new link was sent." });
    }

    /// <summary>
    /// POST api/auth/refresh
    /// Reads the refresh token from the HttpOnly cookie, exchanges it for a new
    /// access token (rotating the refresh token in the cookie). Lets the client
    /// stay signed in past the access token's 1-hour expiry. No request body —
    /// the browser sends the cookie automatically.
    /// </summary>
    /// <response code="200">New access token returned; refresh cookie rotated</response>
    /// <response code="401">Refresh cookie missing, invalid, expired, or already used</response>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Refresh()
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { error = "No active session." });
        }

        try
        {
            var response = await _authService.RefreshAsync(refreshToken);
            return OkWithRefreshCookie(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("API Error: Token refresh failed");
            ClearRefreshCookie();
            return Unauthorized(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST api/auth/logout
    /// Revokes the refresh token carried in the cookie and clears the cookie.
    /// Always returns 200 (a missing/unknown token is treated the same — no probing).
    /// </summary>
    [HttpPost("logout")]
    public async Task<ActionResult> Logout()
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authService.RevokeRefreshTokenAsync(refreshToken);
        }
        ClearRefreshCookie();
        return Ok(new { message = "Logged out." });
    }

    // ── Refresh-token cookie helpers ─────────────────────────────────────────

    private const string RefreshCookieName = "refreshToken";

    /// <summary>
    /// Moves the refresh token from the response body into an HttpOnly cookie
    /// (JS can never read it, so it survives XSS) and returns the rest of the
    /// auth response. Scoped to /api/auth so it is only ever sent to auth calls.
    /// </summary>
    private ActionResult<AuthResponseDto> OkWithRefreshCookie(AuthResponseDto response)
    {
        Response.Cookies.Append(RefreshCookieName, response.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,            // set on HTTPS (prod); relaxed on http dev
            SameSite = SameSiteMode.Lax,         // same-site app → CSRF-safe, still sent on our XHR
            Path = "/api/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        // Never expose the refresh token in the JSON body — it lives only in the cookie
        response.RefreshToken = string.Empty;
        return Ok(response);
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth"
        });
    }
}
