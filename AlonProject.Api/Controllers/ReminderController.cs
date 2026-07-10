using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlonProject.Api.Controllers;

/// <summary>
/// REST API controller for personal calendar reminders.
/// SECURITY: every operation is scoped to the authenticated user — reminders
/// are private and never shared between accounts.
/// Route: /api/reminder
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ReminderController : ControllerBase
{
    private readonly IReminderService _service;
    private readonly ILogger<ReminderController> _logger;

    public ReminderController(IReminderService service, ILogger<ReminderController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Extracts the authenticated user's ID from the NameIdentifier claim.
    /// </summary>
    private int? GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
        {
            return null;
        }
        return userId;
    }

    /// <summary>
    /// GET api/reminder
    /// Retrieves all reminders of the current user, ordered by date.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReminderDto>>> GetMine()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        var reminders = await _service.GetForUserAsync(userId.Value);
        return Ok(reminders);
    }

    /// <summary>
    /// POST api/reminder
    /// Creates a reminder for the current user.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ReminderDto>> Create(CreateReminderDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        var created = await _service.CreateAsync(userId.Value, dto);
        _logger.LogInformation("API Response: Reminder {ReminderId} created for user {UserId}", created.Id, userId);
        return CreatedAtAction(nameof(GetMine), null, created);
    }

    /// <summary>
    /// DELETE api/reminder/{id}
    /// Deletes one of the current user's reminders.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        var deleted = await _service.DeleteAsync(id, userId.Value);
        if (!deleted)
        {
            return NotFound(new { error = "Reminder not found." });
        }

        return NoContent();
    }
}
