using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlonProject.Api.Controllers;

/// <summary>
/// REST API controller for Transaction (stock movement) management.
/// Handles all inventory transactions including sales, returns, adjustments, and audit trail.
/// HIERARCHY: Transaction visibility follows the warehouse tree — users only see transactions
/// for items in warehouses they can access (owners: owned trees; employees: assigned tree).
/// Route: /api/transaction
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TransactionController : ControllerBase
{
    private readonly ITransactionService _service;
    private readonly IItemService _itemService;
    private readonly IWarehouseAccessService _accessService;
    private readonly ILogger<TransactionController> _logger;

    public TransactionController(
        ITransactionService service,
        IItemService itemService,
        IWarehouseAccessService accessService,
        ILogger<TransactionController> logger)
    {
        _service = service;
        _itemService = itemService;
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
    /// Runs the centralized DB-based access check against the warehouse of the given item.
    /// </summary>
    private async Task<bool> CanAccessItemWarehouseAsync(int itemId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return false;
        }

        var item = await _itemService.GetByIdAsync(itemId);
        if (item == null)
        {
            // Let endpoint logic handle not-found separately; access is denied by default
            return false;
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        return await _accessService.CanAccessWarehouseAsync(item.WarehouseId, userId.Value, role, GetCurrentUserWarehouseId());
    }

    /// <summary>
    /// GET api/transaction/{id}
    /// Retrieves a single transaction record by its unique ID.
    /// SECURITY: The transaction's item must be in a warehouse the user can access (DB-checked).
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TransactionDto>> GetById(int id)
    {
        _logger.LogInformation("API Request: GET transaction by ID: {TransactionId}", id);
        var transaction = await _service.GetByIdAsync(id);
        if (transaction == null)
        {
            _logger.LogWarning("API Response: Transaction not found. ID: {TransactionId}", id);
            return NotFound();
        }

        // SECURITY: Verify the transaction's item warehouse is within the user's accessible tree
        if (!await CanAccessItemWarehouseAsync(transaction.ItemId))
        {
            _logger.LogWarning("API Error: Transaction {TransactionId} is outside user's accessible warehouses", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Transaction belongs to a warehouse you cannot access." });
        }

        _logger.LogDebug("API Response: Transaction returned. ID: {TransactionId}", id);
        return Ok(transaction);
    }

    /// <summary>
    /// GET api/transaction
    /// Retrieves all transactions visible to the user, ordered by creation date (newest first).
    /// HIERARCHY: Admin sees transactions across all owned warehouse trees; employees see their assigned tree.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransactionDto>>> GetAll()
    {
        _logger.LogInformation("API Request: GET all transactions");

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid authentication token." });
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var warehouseId = GetCurrentUserWarehouseId();

        _logger.LogDebug("User claims - UserId: {UserId}, Role: {Role}, WarehouseId: {WarehouseId}", userId, role, warehouseId);

        var transactions = await _service.GetAllForUserAsync(userId.Value, role, warehouseId);
        _logger.LogInformation("API Response: Returned {TransactionCount} transactions", transactions.Count());
        return Ok(transactions);
    }


    /// <summary>
    /// GET api/transaction/item/{itemId}
    /// Retrieves all transactions (audit trail) for a specific inventory item.
    /// SECURITY: The item must be in a warehouse the user can access (DB-checked).
    /// </summary>
    [HttpGet("item/{itemId}")]
    public async Task<ActionResult<IEnumerable<TransactionDto>>> GetByItemId(int itemId)
    {
        _logger.LogInformation("API Request: GET transaction history for ItemId: {ItemId}", itemId);

        // SECURITY: Verify the item's warehouse is within the user's accessible tree
        if (!await CanAccessItemWarehouseAsync(itemId))
        {
            _logger.LogWarning("API Error: Item {ItemId} is outside user's accessible warehouses", itemId);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Item belongs to a warehouse you cannot access." });
        }

        var transactions = await _service.GetByItemIdAsync(itemId);
        _logger.LogInformation("API Response: Returned {TransactionCount} transactions for ItemId: {ItemId}",
            transactions.Count(), itemId);
        return Ok(transactions);
    }

    /// <summary>
    /// POST api/transaction
    /// Creates a new stock transaction (sale, return, adjustment) and updates item quantity atomically.
    /// Validates stock availability before allowing negative movements.
    /// SECURITY: The target item must be in a warehouse the user can access (DB-checked).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create(CreateTransactionDto dto)
    {
        _logger.LogInformation(
            "API Request: POST create transaction. ItemId: {ItemId}, Type: {TransactionType}, Quantity: {Quantity}",
            dto.ItemId, dto.Type, dto.Quantity);

        // SECURITY: Verify the target item is within the user's accessible tree before mutating stock
        if (!await CanAccessItemWarehouseAsync(dto.ItemId))
        {
            _logger.LogWarning("API Error: User cannot create transaction for item {ItemId} (outside accessible warehouses)", dto.ItemId);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Item belongs to a warehouse you cannot access." });
        }

        try
        {
            var transaction = await _service.CreateAsync(dto);
            _logger.LogInformation(
                "API Response: Transaction created successfully. ID: {TransactionId}, ItemId: {ItemId}",
                transaction.Id, dto.ItemId);
            return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, transaction);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("API Response: Transaction creation failed - item not found. Error: {ErrorMessage}",
                ex.Message);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("API Response: Transaction creation failed - stock validation error. Error: {ErrorMessage}",
                ex.Message);
            return BadRequest(ex.Message);
        }
    }
}
