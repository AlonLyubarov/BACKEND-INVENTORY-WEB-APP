using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using AlonProject.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlonProject.Api.Controllers;

/// <summary>
/// Work-schedule endpoints.
/// SECURITY:
/// - Every user reads THEIR OWN schedule (GET mine).
/// - Only the warehouse OWNER (Admin, DB-checked) manages a warehouse's
///   schedule: list, assign, delete.
/// Route: /api/shift
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ShiftController : ControllerBase
{
    private readonly IShiftService _service;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ILogger<ShiftController> _logger;

    public ShiftController(
        IShiftService service,
        IWarehouseRepository warehouseRepository,
        ILogger<ShiftController> logger)
    {
        _service = service;
        _warehouseRepository = warehouseRepository;
        _logger = logger;
    }

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
    /// GET api/shift/mine
    /// The calling user's upcoming shifts — every role sees their own schedule.
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<ShiftDto>>> GetMine()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        var shifts = await _service.GetMineAsync(userId.Value);
        return Ok(shifts);
    }

    /// <summary>
    /// GET api/shift/warehouse/{warehouseId}
    /// Upcoming schedule of a warehouse. Owner only (DB-checked).
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("warehouse/{warehouseId}")]
    public async Task<ActionResult<IEnumerable<ShiftDto>>> GetForWarehouse(int warehouseId)
    {
        var userId = GetCurrentUserId();
        if (userId == null || !await _warehouseRepository.IsOwnerAsync(warehouseId, userId.Value))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "You do not have access to this warehouse's schedule." });
        }

        var shifts = await _service.GetForWarehouseAsync(warehouseId);
        return Ok(shifts);
    }

    /// <summary>
    /// POST api/shift
    /// Assigns a shift to a team member of the caller's warehouse. Owner only.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ShiftDto>> Create([FromBody] CreateShiftDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null || !await _warehouseRepository.IsOwnerAsync(dto.WarehouseId, userId.Value))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "You do not have access to this warehouse's schedule." });
        }

        try
        {
            var created = await _service.CreateAsync(dto);
            _logger.LogInformation("API Response: Shift {ShiftId} assigned", created.Id);
            return CreatedAtAction(nameof(GetMine), null, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// DELETE api/shift/{id}
    /// Removes a shift from the schedule. Owner of the shift's warehouse only.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        var warehouseId = await _service.GetShiftWarehouseIdAsync(id);
        if (warehouseId == null)
        {
            return NotFound(new { error = "Shift not found." });
        }
        if (userId == null || !await _warehouseRepository.IsOwnerAsync(warehouseId.Value, userId.Value))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "You do not have access to this warehouse's schedule." });
        }

        await _service.DeleteAsync(id);
        return NoContent();
    }
}
