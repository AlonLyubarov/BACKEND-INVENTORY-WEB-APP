using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlonProject.Api.Controllers;

/// <summary>
/// Self-service account operations for the authenticated user.
/// Deliberately NOT under /api/auth (anonymous zone) — these endpoints
/// require the caller's JWT.
/// Route: /api/account
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IAuthService authService, ILogger<AccountController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// POST api/account/delete
    /// Permanently deletes the caller's account after password confirmation.
    /// Owners (Admin) cascade: warehouses, items, transactions, product
    /// catalog, and all invited team accounts are removed with them.
    /// NOTE: wrong password returns 400 (not 401) so the client session
    /// isn't treated as expired.
    /// </summary>
    /// <response code="204">Account and owned data deleted</response>
    /// <response code="400">Password incorrect</response>
    [HttpPost("delete")]
    public async Task<ActionResult> DeleteAccount([FromBody] DeleteAccountDto dto)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        _logger.LogInformation("API Request: POST account/delete for user {UserId}", userId);

        try
        {
            await _authService.DeleteAccountAsync(userId, dto.Password);
            _logger.LogInformation("API Response: Account {UserId} deleted", userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            // Wrong password — 400 on purpose (401 would log the client out)
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "User not found." });
        }
    }
}
