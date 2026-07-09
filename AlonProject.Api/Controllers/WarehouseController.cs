using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using AlonProject.Domain.Enums;
using AlonProject.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlonProject.Api.Controllers;

/// <summary>
/// REST API controller for Warehouse management.
/// Hierarchy model: Admins OWN main warehouses (Warehouse.OwnerId); each main warehouse
/// can have one level of sub-warehouses (Warehouse.ParentWarehouseId).
/// Access rule (single source of truth = IWarehouseAccessService):
/// - Admin: may access only warehouse trees they own.
/// - Employee/ShiftManager: may access only their assigned main warehouse tree (read endpoints).
/// Route: /api/warehouse
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly IWarehouseService _service;
    private readonly IWarehouseAccessService _accessService;
    private readonly IUserRepository _userRepository;
    private readonly IAuthService _authService;
    private readonly ILogger<WarehouseController> _logger;

    public WarehouseController(
        IWarehouseService service,
        IWarehouseAccessService accessService,
        IUserRepository userRepository,
        IAuthService authService,
        ILogger<WarehouseController> logger)
    {
        _service = service;
        _accessService = accessService;
        _userRepository = userRepository;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Extracts the authenticated user's ID from the NameIdentifier claim.
    /// Returns null when the claim is missing/invalid (should not happen for [Authorize] endpoints).
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
    /// Extracts the user's assigned warehouse ID from the WarehouseId claim (employees only).
    /// </summary>
    private int? GetCurrentUserWarehouseId()
    {
        var warehouseIdClaim = User.FindFirst("WarehouseId");
        if (warehouseIdClaim == null || !int.TryParse(warehouseIdClaim.Value, out var warehouseId))
        {
            return null;
        }
        return warehouseId;
    }

    /// <summary>
    /// Runs the centralized DB-based access check for the current user against a warehouse node.
    /// </summary>
    private async Task<bool> CanAccessAsync(int warehouseId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return false;
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        return await _accessService.CanAccessWarehouseAsync(warehouseId, userId.Value, role, GetCurrentUserWarehouseId());
    }

    /// <summary>
    /// GET api/warehouse/public-list
    /// Retrieves minimal warehouse information for public/registration endpoints.
    /// No authentication required.
    /// Returns only Id and Name fields (no sensitive metadata).
    /// </summary>
    /// <response code="200">Public warehouse list returned</response>
    [AllowAnonymous]
    [HttpGet("public-list")]
    public async Task<ActionResult<IEnumerable<object>>> GetPublicList()
    {
        _logger.LogInformation("API Request: GET public warehouse list (unauthenticated)");
        try
        {
            var warehouses = await _service.GetAllAsync();
            var publicList = warehouses.Select(w => new 
            {
                w.Id,
                w.Name
            }).ToList();

            _logger.LogInformation("API Response: Returned {WarehouseCount} public warehouse entries", publicList.Count);
            return Ok(publicList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Error: Failed to retrieve public warehouse list");
            return StatusCode(500, new { error = "Internal server error while fetching warehouse list." });
        }
    }

    /// <summary>
    /// GET api/warehouse/{id}
    /// Retrieves a single warehouse node (main or sub) by ID.
    /// Access checked against the DB: owners see their tree; employees see their assigned tree.
    /// </summary>
    /// <response code="200">Warehouse found</response>
    /// <response code="403">User does not have access to this warehouse</response>
    /// <response code="404">Warehouse not found</response>
    [HttpGet("{id}")]
    public async Task<ActionResult<WarehouseDto>> GetById(int id)
    {
        _logger.LogInformation("API Request: GET warehouse by ID: {WarehouseId}", id);

        if (!await CanAccessAsync(id))
        {
            _logger.LogWarning("API Error: User does not have access to warehouse ID: {WarehouseId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to this warehouse." });
        }

        var warehouse = await _service.GetByIdAsync(id);
        if (warehouse == null)
        {
            _logger.LogWarning("API Response: Warehouse not found - ID: {WarehouseId}", id);
            return NotFound();
        }
        _logger.LogDebug("API Response: Warehouse returned - ID: {WarehouseId}, Name: {Name}", id, warehouse.Name);
        return Ok(warehouse);
    }

    /// <summary>
    /// GET api/warehouse
    /// Admin: returns all MAIN warehouses OWNED by the authenticated user (with sub-warehouses).
    /// Employee/ShiftManager: returns their single assigned main warehouse.
    /// </summary>
    /// <response code="200">Owned/assigned warehouses returned</response>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WarehouseDto>>> GetAll()
    {
        _logger.LogInformation("API Request: GET warehouses for authenticated user");

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        try
        {
            if (User.IsInRole("Admin"))
            {
                // Owner: list main warehouses they own, each including its sub-warehouses
                var owned = await _service.GetByOwnerAsync(userId.Value);
                _logger.LogInformation("API Response: Returned {WarehouseCount} owned warehouse(s) for user {UserId}", owned.Count(), userId);
                return Ok(owned);
            }

            // Employee/ShiftManager: return their assigned main warehouse (if any)
            var assignedWarehouseId = GetCurrentUserWarehouseId();
            if (assignedWarehouseId == null)
            {
                _logger.LogInformation("API Response: User {UserId} has no warehouse assignment", userId);
                return Ok(Array.Empty<WarehouseDto>());
            }

            var warehouse = await _service.GetByIdAsync(assignedWarehouseId.Value);
            if (warehouse == null)
            {
                _logger.LogWarning("API Response: Assigned warehouse not found - ID: {WarehouseId}", assignedWarehouseId);
                return NotFound(new { error = "Your assigned warehouse was not found." });
            }

            return Ok(new[] { warehouse });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Error: Failed to retrieve warehouses");
            return StatusCode(500, new { error = "Internal server error while fetching warehouses." });
        }
    }

    /// <summary>
    /// GET api/warehouse/{id}/sub
    /// Retrieves the direct sub-warehouses of a warehouse node.
    /// Access checked via the centralized rule.
    /// </summary>
    /// <response code="200">Sub-warehouses returned</response>
    /// <response code="403">User does not have access to this warehouse</response>
    [HttpGet("{id}/sub")]
    public async Task<ActionResult<IEnumerable<WarehouseDto>>> GetSubWarehouses(int id)
    {
        _logger.LogInformation("API Request: GET sub-warehouses of warehouse {WarehouseId}", id);

        if (!await CanAccessAsync(id))
        {
            _logger.LogWarning("API Error: User does not have access to warehouse ID: {WarehouseId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to this warehouse." });
        }

        var subs = await _service.GetSubWarehousesAsync(id);
        _logger.LogInformation("API Response: Returned {SubCount} sub-warehouses for warehouse {WarehouseId}", subs.Count(), id);
        return Ok(subs);
    }

    /// <summary>
    /// GET api/warehouse/{id}/details
    /// Retrieves comprehensive warehouse details including:
    /// - Warehouse information
    /// - All items in the warehouse node and its sub-warehouses
    /// - All transactions for those items
    /// - Inventory summary statistics
    /// Access checked via the centralized DB-based rule.
    /// </summary>
    /// <response code="200">Warehouse details returned</response>
    /// <response code="403">User does not have access to this warehouse</response>
    /// <response code="404">Warehouse not found</response>
    [HttpGet("{id}/details")]
    public async Task<ActionResult<WarehouseDetailsDto>> GetWarehouseDetails(int id)
    {
        _logger.LogInformation("API Request: GET warehouse details - ID: {WarehouseId}", id);

        if (!await CanAccessAsync(id))
        {
            _logger.LogWarning("API Error: User does not have access to warehouse ID: {WarehouseId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to this warehouse." });
        }

        var details = await _service.GetWarehouseDetailsAsync(id);
        if (details == null)
        {
            _logger.LogWarning("API Response: Warehouse not found for details - ID: {WarehouseId}", id);
            return NotFound();
        }
        _logger.LogInformation("API Response: Warehouse details returned - ID: {WarehouseId}, Items: {ItemCount}", id, details.Items.Count());
        return Ok(details);
    }

    /// <summary>
    /// GET api/warehouse/{id}/items
    /// Retrieves all items in a warehouse node and its sub-warehouses.
    /// Access checked via the centralized DB-based rule.
    /// </summary>
    [HttpGet("{id}/items")]
    public async Task<ActionResult<IEnumerable<ItemDto>>> GetWarehouseItems(int id)
    {
        _logger.LogInformation("API Request: GET warehouse items - WarehouseID: {WarehouseId}", id);

        if (!await CanAccessAsync(id))
        {
            _logger.LogWarning("API Error: User does not have access to warehouse ID: {WarehouseId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to this warehouse." });
        }

        var items = await _service.GetWarehouseItemsAsync(id);
        _logger.LogInformation("API Response: Returned {ItemCount} items for warehouse ID: {WarehouseId}", items.Count(), id);
        return Ok(items);
    }

    /// <summary>
    /// GET api/warehouse/{id}/transactions
    /// Retrieves all transactions for items in a warehouse node and its sub-warehouses.
    /// Access checked via the centralized DB-based rule.
    /// </summary>
    [HttpGet("{id}/transactions")]
    public async Task<ActionResult<IEnumerable<TransactionDto>>> GetWarehouseTransactions(int id)
    {
        _logger.LogInformation("API Request: GET warehouse transactions - WarehouseID: {WarehouseId}", id);

        if (!await CanAccessAsync(id))
        {
            _logger.LogWarning("API Error: User does not have access to warehouse ID: {WarehouseId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to this warehouse." });
        }

        var transactions = await _service.GetWarehouseTransactionsAsync(id);
        _logger.LogInformation("API Response: Returned {TransactionCount} transactions for warehouse ID: {WarehouseId}", transactions.Count(), id);
        return Ok(transactions);
    }

    /// <summary>
    /// POST api/warehouse
    /// Creates a new MAIN warehouse owned by the authenticated Admin.
    /// Frontend sends: { name: string, location: string }
    /// Requires: Admin role (owners only).
    /// </summary>
    /// <response code="201">Warehouse created successfully</response>
    /// <response code="400">Invalid warehouse data</response>
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<WarehouseDto>> Create([FromBody] CreateWarehouseDto dto)
    {
        _logger.LogInformation("API Request: POST create warehouse - Name: {WarehouseName}, Location: {Location}",
            dto.Name, dto.Location);

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        try
        {
            var warehouse = await _service.CreateAsync(dto, userId.Value);
            _logger.LogInformation("API Response: Main warehouse created - ID: {WarehouseId}, Name: {WarehouseName}, Owner: {OwnerId}",
                warehouse.Id, dto.Name, userId);
            return CreatedAtAction(nameof(GetById), new { id = warehouse.Id }, warehouse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Error: Warehouse creation failed - {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST api/warehouse/{id}/sub
    /// Creates a SUB-warehouse under the given MAIN warehouse.
    /// Rules enforced:
    /// - Caller must be the owner of the parent warehouse (DB-checked).
    /// - Parent must be a main warehouse (one nesting level only) � enforced by the service.
    /// </summary>
    /// <response code="201">Sub-warehouse created successfully</response>
    /// <response code="400">Parent is a sub-warehouse (nesting rejected) or invalid data</response>
    /// <response code="403">Caller does not own the parent warehouse</response>
    /// <response code="404">Parent warehouse not found</response>
    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/sub")]
    public async Task<ActionResult<WarehouseDto>> CreateSubWarehouse(int id, [FromBody] CreateWarehouseDto dto)
    {
        _logger.LogInformation("API Request: POST create sub-warehouse under {ParentId} - Name: {Name}", id, dto.Name);

        if (!await CanAccessAsync(id))
        {
            _logger.LogWarning("API Error: User does not own parent warehouse ID: {ParentId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to this warehouse." });
        }

        try
        {
            var created = await _service.CreateSubWarehouseAsync(id, dto);
            _logger.LogInformation("API Response: Sub-warehouse created - ID: {WarehouseId}, Parent: {ParentId}", created.Id, id);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("API Error: Parent warehouse not found - ID: {ParentId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // One-level nesting violation
            _logger.LogWarning("API Error: Sub-warehouse nesting rejected - {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Error: Sub-warehouse creation failed - {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// PUT api/warehouse/{id}
    /// Updates an existing warehouse node (main or sub).
    /// Frontend sends: { name: string, location: string }
    /// Requires: Admin role and OWNERSHIP of the warehouse tree (DB-checked).
    /// </summary>
    /// <response code="200">Warehouse updated successfully</response>
    /// <response code="403">Caller does not own this warehouse</response>
    /// <response code="404">Warehouse not found</response>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<WarehouseDto>> Update(int id, [FromBody] CreateWarehouseDto dto)
    {
        _logger.LogInformation("API Request: PUT update warehouse - ID: {WarehouseId}, Name: {WarehouseName}", id, dto.Name);

        // Owner-only: verified against the DB (root warehouse ownership)
        if (!await CanAccessAsync(id))
        {
            _logger.LogWarning("API Error: User does not have access to warehouse ID: {WarehouseId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to this warehouse." });
        }

        try
        {
            var warehouse = await _service.UpdateAsync(id, dto);
            if (warehouse == null)
            {
                _logger.LogWarning("API Response: Warehouse not found for update - ID: {WarehouseId}", id);
                return NotFound();
            }
            _logger.LogInformation("API Response: Warehouse updated successfully - ID: {WarehouseId}", id);
            return Ok(warehouse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Error: Warehouse update failed - {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// DELETE api/warehouse/{id}
    /// Deletes a warehouse node (main or sub).
    /// Requires: Admin role and OWNERSHIP of the warehouse tree (DB-checked).
    /// Rules: a warehouse with sub-warehouses cannot be deleted (400) � delete/move children first.
    /// </summary>
    /// <response code="204">Warehouse deleted successfully</response>
    /// <response code="400">Warehouse has sub-warehouses and cannot be deleted</response>
    /// <response code="403">Caller does not own this warehouse</response>
    /// <response code="404">Warehouse not found</response>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        _logger.LogInformation("API Request: DELETE warehouse - ID: {WarehouseId}", id);

        // Owner-only: verified against the DB (root warehouse ownership)
        if (!await CanAccessAsync(id))
        {
            _logger.LogWarning("API Error: User does not have access to warehouse ID: {WarehouseId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to this warehouse." });
        }

        try
        {
            var success = await _service.DeleteAsync(id);
            if (!success)
            {
                _logger.LogWarning("API Response: Warehouse not found for deletion - ID: {WarehouseId}", id);
                return NotFound();
            }
            _logger.LogInformation("API Response: Warehouse deleted successfully - ID: {WarehouseId}", id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            // Has sub-warehouses � clear 400 instead of an FK error
            _logger.LogWarning("API Error: Warehouse deletion rejected - {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Error: Warehouse deletion failed - {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST api/warehouse/{id}/invite
    /// Invites (creates) a new Employee/ShiftManager user into the given MAIN warehouse.
    /// SECURITY:
    /// - Caller must be the OWNER of the warehouse (DB-checked).
    /// - Role is restricted to Employee/ShiftManager � Admin invitations are rejected (400).
    /// - Target must be a MAIN warehouse (employees belong to the main warehouse, not sub-warehouses).
    /// Replaces the legacy assign-admin/promote-to-admin flows.
    /// </summary>
    /// <param name="id">The main warehouse to invite the user into</param>
    /// <param name="dto">Invitation details (username, email, password, role)</param>
    /// <response code="201">User created and assigned to the warehouse</response>
    /// <response code="400">Invalid role (Admin), duplicate username, or target is a sub-warehouse</response>
    /// <response code="403">Caller does not own this warehouse</response>
    /// <response code="404">Warehouse not found</response>
    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/invite")]
    public async Task<ActionResult<UserDto>> InviteUser(int id, [FromBody] InviteUserDto dto)
    {
        _logger.LogInformation("API Request: POST invite user {Username} as {Role} to warehouse {WarehouseId}",
            dto.Username, dto.Role, id);

        // SECURITY: Owner-only, DB-checked
        if (!await CanAccessAsync(id))
        {
            _logger.LogWarning("API Error: User does not own warehouse ID: {WarehouseId} - invite rejected", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to this warehouse." });
        }

        // Verify the warehouse exists and is a MAIN warehouse
        var warehouse = await _service.GetByIdAsync(id);
        if (warehouse == null)
        {
            _logger.LogWarning("API Error: Warehouse not found - ID: {WarehouseId}", id);
            return NotFound(new { error = "Warehouse not found." });
        }

        if (warehouse.ParentWarehouseId != null)
        {
            _logger.LogWarning("API Error: Cannot invite users into a sub-warehouse - ID: {WarehouseId}", id);
            return BadRequest(new { error = "Users can only be invited into a main warehouse, not a sub-warehouse." });
        }

        try
        {
            var created = await _authService.InviteUserAsync(dto, id);
            _logger.LogInformation("API Response: User {UserId} ({Username}) invited to warehouse {WarehouseId} as {Role}",
                created.Id, created.Username, id, created.Role);
            return CreatedAtAction(nameof(GetWarehouseUsers), new { id }, created);
        }
        catch (InvalidOperationException ex)
        {
            // Duplicate username or Admin role rejection
            _logger.LogWarning("API Error: Invite failed - {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Error: Invite failed unexpectedly - {Message}", ex.Message);
            return StatusCode(500, new { error = "An unexpected error occurred while inviting the user." });
        }
    }

    /// <summary>
    /// GET api/warehouse/{id}/users
    /// Retrieves all users assigned to the given MAIN warehouse.
    /// SECURITY: Caller must be the OWNER of the warehouse (DB-checked).
    /// </summary>
    /// <response code="200">Users returned</response>
    /// <response code="403">Caller does not own this warehouse</response>
    /// <response code="404">Warehouse not found</response>
    [Authorize(Roles = "Admin")]
    [HttpGet("{id}/users")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetWarehouseUsers(int id)
    {
        _logger.LogInformation("API Request: GET users of warehouse {WarehouseId}", id);

        // SECURITY: Owner-only, DB-checked
        if (!await CanAccessAsync(id))
        {
            _logger.LogWarning("API Error: User does not own warehouse ID: {WarehouseId} - user list rejected", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to this warehouse." });
        }

        var warehouse = await _service.GetByIdAsync(id);
        if (warehouse == null)
        {
            _logger.LogWarning("API Error: Warehouse not found - ID: {WarehouseId}", id);
            return NotFound(new { error = "Warehouse not found." });
        }

        var users = await _userRepository.GetByWarehouseIdAsync(id);
        var result = users.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            Role = u.Role,
            WarehouseId = u.WarehouseId,
            WarehouseName = u.Warehouse?.Name
        }).ToList();

        _logger.LogInformation("API Response: Returned {UserCount} users for warehouse {WarehouseId}", result.Count, id);
        return Ok(result);
    }

    /// <summary>
    /// DELETE api/warehouse/{id}/users/{userId}
    /// Removes an invited user (Employee/ShiftManager) from the given MAIN warehouse,
    /// deleting their account. Owner accounts (Admin) cannot be removed this way.
    /// SECURITY: Caller must be the OWNER of the warehouse (DB-checked).
    /// </summary>
    /// <response code="204">User removed</response>
    /// <response code="400">Target user is an Admin</response>
    /// <response code="403">Caller does not own this warehouse</response>
    /// <response code="404">User not found in this warehouse</response>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}/users/{userId}")]
    public async Task<ActionResult> RemoveWarehouseUser(int id, int userId)
    {
        _logger.LogInformation("API Request: DELETE user {UserId} from warehouse {WarehouseId}", userId, id);

        // SECURITY: Owner-only, DB-checked
        if (!await CanAccessAsync(id))
        {
            _logger.LogWarning("API Error: User does not own warehouse ID: {WarehouseId} - user removal rejected", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to this warehouse." });
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.WarehouseId != id)
        {
            _logger.LogWarning("API Error: User {UserId} not found in warehouse {WarehouseId}", userId, id);
            return NotFound(new { error = "User not found in this warehouse." });
        }

        if (user.Role == UserRole.Admin)
        {
            _logger.LogWarning("API Error: Attempt to remove Admin user {UserId} from warehouse {WarehouseId}", userId, id);
            return BadRequest(new { error = "Owner (Admin) accounts cannot be removed from a warehouse." });
        }

        await _userRepository.DeleteAsync(userId);
        _logger.LogInformation("API Response: User {UserId} removed from warehouse {WarehouseId}", userId, id);
        return NoContent();
    }
}
