using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlonProject.Api.Controllers;

/// <summary>
/// REST API controller for Item (stock record) management.
/// Handles inventory items per location with quantity tracking and product associations.
/// HIERARCHY: Item visibility is scoped to the user's warehouse tree:
/// - Admin (owner): items in ALL owned main warehouses + their sub-warehouses (DB-resolved).
/// - Employee/ShiftManager: items in their assigned main warehouse + its sub-warehouses.
/// Route: /api/item
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ItemController : ControllerBase
{
    private readonly IItemService _service;
    private readonly IWarehouseAccessService _accessService;
    private readonly ILogger<ItemController> _logger;

    public ItemController(IItemService service, IWarehouseAccessService accessService, ILogger<ItemController> logger)
    {
        _service = service;
        _accessService = accessService;
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
    /// Runs the centralized DB-based access check against a warehouse node.
    /// </summary>
    private async Task<bool> CanAccessWarehouseAsync(int warehouseId)
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
    /// GET api/item/{id}
    /// Retrieves a single inventory item by its unique ID.
    /// SECURITY: Verifies the item's warehouse belongs to the user's accessible tree (DB-checked).
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ItemDto>> GetById(int id)
    {
        _logger.LogInformation("API Request: GET item by ID: {ItemId}", id);

        var item = await _service.GetByIdAsync(id);
        if (item == null)
        {
            _logger.LogWarning("API Response: Item not found. ID: {ItemId}", id);
            return NotFound();
        }

        // SECURITY: Verify item's warehouse is within the user's accessible tree
        if (!await CanAccessWarehouseAsync(item.WarehouseId))
        {
            _logger.LogWarning("API Error: Item {ItemId} in warehouse {ItemWarehouse} is outside user's accessible tree",
                id, item.WarehouseId);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Item belongs to a warehouse you cannot access." });
        }

        _logger.LogDebug("API Response: Item returned. ID: {ItemId}", id);
        return Ok(item);
    }

    /// <summary>
    /// GET api/item
    /// Retrieves all inventory items visible to the user.
    /// HIERARCHY: Admin sees items across all owned warehouse trees; employees see their assigned tree.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ItemDto>>> GetAll()
    {
        _logger.LogInformation("API Request: GET all items");

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var warehouseId = GetCurrentUserWarehouseId();

        _logger.LogDebug("User claims - UserId: {UserId}, Role: {Role}, WarehouseId: {WarehouseId}", userId, role, warehouseId);

        var items = await _service.GetAllForUserAsync(userId.Value, role, warehouseId);
        _logger.LogInformation("API Response: Returned {ItemCount} items", items.Count());
        return Ok(items);
    }


    /// <summary>
    /// GET api/item/product/{productCatalogId}
    /// Retrieves all inventory items for a specific product catalog entry.
    /// HIERARCHY: Scoped to the user's accessible warehouse tree(s).
    /// </summary>
    [HttpGet("product/{productCatalogId}")]
    public async Task<ActionResult<IEnumerable<ItemDto>>> GetByProductCatalogId(int productCatalogId)
    {
        _logger.LogInformation("API Request: GET items by ProductCatalogId: {ProductCatalogId}", productCatalogId);

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var items = await _service.GetByProductCatalogIdForUserAsync(productCatalogId, userId.Value, role, GetCurrentUserWarehouseId());
        _logger.LogInformation("API Response: Returned {ItemCount} items for ProductCatalogId: {ProductCatalogId}",
            items.Count(), productCatalogId);
        return Ok(items);
    }

    /// <summary>
    /// GET api/item/location/{location}
    /// Retrieves all inventory items at a specific warehouse or distribution location.
    /// HIERARCHY: Scoped to the user's accessible warehouse tree(s).
    /// </summary>
    [HttpGet("location/{location}")]
    public async Task<ActionResult<IEnumerable<ItemDto>>> GetByLocation(string location)
    {
        _logger.LogInformation("API Request: GET items by location: {Location}", location);

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var items = await _service.GetByLocationForUserAsync(location, userId.Value, role, GetCurrentUserWarehouseId());
        _logger.LogInformation("API Response: Returned {ItemCount} items at location: {Location}",
            items.Count(), location);
        return Ok(items);
    }

    /// <summary>
    /// POST api/item
    /// Creates a new inventory item (stock record) for a product at a specific location.
    /// SECURITY: Target warehouse must be within the caller's accessible tree (DB-checked).
    /// Admin (owner): any owned warehouse node; ShiftManager: their assigned tree.
    /// If TargetWarehouseId is not provided, falls back to the caller's assigned warehouse claim.
    /// </summary>
    [Authorize(Roles = "Admin,ShiftManager")]
    [HttpPost]
    public async Task<ActionResult<ItemDto>> Create(CreateItemDto dto)
    {
        _logger.LogInformation(
            "API Request: POST create item. ProductCatalogId: {ProductCatalogId}, Location: {Location}, Quantity: {Quantity}, TargetWarehouseId: {TargetWarehouseId}",
            dto.ProductCatalogId, dto.Location, dto.Quantity, dto.TargetWarehouseId);

        // Resolve the destination warehouse: explicit target or the caller's assigned warehouse
        var targetWarehouseId = dto.TargetWarehouseId ?? GetCurrentUserWarehouseId();
        if (targetWarehouseId == null)
        {
            _logger.LogWarning("API Error: No target warehouse resolvable for item creation.");
            return BadRequest(new { error = "A target warehouse is required. Provide targetWarehouseId or contact your administrator." });
        }

        // SECURITY: DB-checked access to the destination warehouse node
        if (!await CanAccessWarehouseAsync(targetWarehouseId.Value))
        {
            _logger.LogWarning("API Error: User cannot create items in warehouse {WarehouseId}", targetWarehouseId);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to the target warehouse." });
        }

        try
        {
            var item = await _service.CreateAsync(dto, targetWarehouseId.Value);
            _logger.LogInformation(
                "API Response: Item created successfully. ID: {ItemId}, ProductCatalogId: {ProductCatalogId}, WarehouseId: {WarehouseId}",
                item.Id, dto.ProductCatalogId, targetWarehouseId);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("API Response: Item creation failed - product not found. Error: {ErrorMessage}", ex.Message);
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// PUT api/item/{id}
    /// Updates an existing inventory item's quantity and location.
    /// SECURITY: Item's warehouse must be within the caller's accessible tree (DB-checked).
    /// Requires: ShiftManager or Admin role
    /// </summary>
    [Authorize(Roles = "ShiftManager,Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<ItemDto>> Update(int id, CreateItemDto dto)
    {
        _logger.LogInformation("API Request: PUT update item. ID: {ItemId}, Location: {Location}, Quantity: {Quantity}",
            id, dto.Location, dto.Quantity);

        // SECURITY: Verify the item is inside the caller's accessible tree before updating
        var existing = await _service.GetByIdAsync(id);
        if (existing == null)
        {
            _logger.LogWarning("API Response: Item not found for update. ID: {ItemId}", id);
            return NotFound();
        }

        if (!await CanAccessWarehouseAsync(existing.WarehouseId))
        {
            _logger.LogWarning("API Error: User cannot update item {ItemId} in warehouse {WarehouseId}", id, existing.WarehouseId);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Item belongs to a warehouse you cannot access." });
        }

        try
        {
            var item = await _service.UpdateAsync(id, dto);
            _logger.LogInformation("API Response: Item updated successfully. ID: {ItemId}", id);
            return Ok(item);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("API Response: Item not found for update. ID: {ItemId}", id);
            return NotFound();
        }
    }

    /// <summary>
    /// DELETE api/item/{id}
    /// Removes an inventory item from the system.
    /// SECURITY: Item's warehouse must be within the caller's owned tree (DB-checked).
    /// Requires: Admin role
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        _logger.LogInformation("API Request: DELETE item. ID: {ItemId}", id);

        // SECURITY: Verify the item is inside the caller's owned tree before deleting
        var existing = await _service.GetByIdAsync(id);
        if (existing == null)
        {
            _logger.LogWarning("API Response: Item not found for deletion. ID: {ItemId}", id);
            return NotFound();
        }

        if (!await CanAccessWarehouseAsync(existing.WarehouseId))
        {
            _logger.LogWarning("API Error: User cannot delete item {ItemId} in warehouse {WarehouseId}", id, existing.WarehouseId);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Item belongs to a warehouse you cannot access." });
        }

        var success = await _service.DeleteAsync(id);
        if (!success)
        {
            _logger.LogWarning("API Response: Item not found for deletion. ID: {ItemId}", id);
            return NotFound();
        }
        _logger.LogInformation("API Response: Item deleted successfully. ID: {ItemId}", id);
        return NoContent();
    }
}
