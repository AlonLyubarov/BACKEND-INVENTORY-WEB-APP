using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlonProject.Api.Controllers;

/// <summary>
/// REST API controller for personal to-do tasks.
/// SECURITY: every operation is scoped to the authenticated user — tasks are
/// private and never shared between accounts.
/// Route: /api/tasks
/// </summary>
[Authorize]
[ApiController]
[Route("api/tasks")]
public class PersonalTaskController : ControllerBase
{
    private readonly IPersonalTaskService _service;
    private readonly ILogger<PersonalTaskController> _logger;

    public PersonalTaskController(IPersonalTaskService service, ILogger<PersonalTaskController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Extracts the authenticated user's ID from the NameIdentifier claim.</summary>
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
    /// GET api/tasks
    /// Retrieves all tasks of the current user (open first, then by due date).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PersonalTaskDto>>> GetMine()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        var tasks = await _service.GetForUserAsync(userId.Value);
        return Ok(tasks);
    }

    /// <summary>
    /// POST api/tasks
    /// Creates a task for the current user.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PersonalTaskDto>> Create(CreatePersonalTaskDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        var created = await _service.CreateAsync(userId.Value, dto);
        _logger.LogInformation("API Response: Personal task {TaskId} created for user {UserId}", created.Id, userId);
        return CreatedAtAction(nameof(GetMine), null, created);
    }

    /// <summary>
    /// PUT api/tasks/{id}
    /// Updates one of the current user's tasks (edit fields or check/uncheck).
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<PersonalTaskDto>> Update(int id, UpdatePersonalTaskDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        var updated = await _service.UpdateAsync(id, userId.Value, dto);
        if (updated == null)
        {
            return NotFound(new { error = "Task not found." });
        }

        return Ok(updated);
    }

    /// <summary>
    /// DELETE api/tasks/{id}
    /// Deletes one of the current user's tasks.
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
            return NotFound(new { error = "Task not found." });
        }

        return NoContent();
    }
}
