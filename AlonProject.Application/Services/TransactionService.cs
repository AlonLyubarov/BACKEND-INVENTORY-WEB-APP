using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using AlonProject.Domain.Entities;
using AlonProject.Domain.Enums;
using AlonProject.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AlonProject.Application.Services;

/// <summary>
/// Service for managing inventory transactions and stock movements.
/// Handles stock adjustments, sales, returns, and audit trail with validation.
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ITransactionRepository transactionRepository,
        IItemRepository itemRepository,
        IWarehouseRepository warehouseRepository,
        ILogger<TransactionService> logger)
    {
        _transactionRepository = transactionRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a specific transaction by ID.
    /// </summary>
    public async Task<TransactionDto?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Retrieving transaction with ID: {TransactionId}", id);
        try
        {
            var entity = await _transactionRepository.GetByIdAsync(id);
            if (entity == null)
            {
                _logger.LogWarning("Transaction not found. ID: {TransactionId}", id);
                return null;
            }
            _logger.LogDebug("Transaction retrieved successfully. ID: {TransactionId}, Type: {TransactionType}",
                id, entity.Type);
            return MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transaction with ID: {TransactionId}", id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all transactions for a specific item, ordered by date (newest first).
    /// </summary>
    public async Task<IEnumerable<TransactionDto>> GetByItemIdAsync(int itemId)
    {
        _logger.LogInformation("Retrieving transaction history for item ID: {ItemId}", itemId);
        try
        {
            var entities = await _transactionRepository.GetByItemIdAsync(itemId);
            var count = entities.Count();
            _logger.LogInformation("Retrieved {TransactionCount} transactions for item {ItemId}", count, itemId);
            return entities.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transactions for item ID: {ItemId}", itemId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all transactions in the system (ordered by date, newest first).
    /// </summary>
    public async Task<IEnumerable<TransactionDto>> GetAllAsync()
    {
        _logger.LogInformation("Retrieving all transactions");
        try
        {
            var entities = await _transactionRepository.GetAllAsync();
            var count = entities.Count();
            _logger.LogInformation("Retrieved {TransactionCount} transactions from system", count);
            return entities.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all transactions");
            throw;
        }
    }

    /// <summary>
    /// Retrieves all transactions filtered by user role and warehouse (security boundary).
    /// Admin users see all transactions; Employee/ShiftManager users see only their warehouse's transactions.
    /// 
    /// SECURITY NOTE: This method implements warehouse-based data isolation for transactions.
    /// Transactions are filtered based on their related Item's warehouse association.
    /// The current Item entity schema does not have a direct WarehouseId property.
    /// For now, this is a placeholder that returns all transactions for Admin (and empty for others).
    /// When the schema is updated to include Item.WarehouseId, filtering logic will apply:
    /// - Admin (role="Admin"): return all transactions
    /// - Employee/ShiftManager: return only transactions where transaction.Item.WarehouseId == user's warehouseId
    /// </summary>
    public async Task<IEnumerable<TransactionDto>> GetAllAsync(string role, int? warehouseId)
    {
        _logger.LogInformation("Retrieving all transactions with warehouse filtering. Role: {Role}, WarehouseId: {WarehouseId}",
            role, warehouseId ?? 0);
        try
        {
            var entities = await _transactionRepository.GetAllAsync();

            // SECURITY BOUNDARY: Filter transactions based on role and warehouse
            if (role == "Admin")
            {
                // Admin sees all transactions
                _logger.LogDebug("Admin user: returning all {TransactionCount} transactions", entities.Count());
            }
            else if (role == "Employee" || role == "ShiftManager")
            {
                // Employee/ShiftManager should see only their warehouse's transactions
                // Once Item.WarehouseId exists, apply: entities = entities.Where(t => t.Item.WarehouseId == warehouseId)
                // For now, without schema support, return empty list as secure default
                _logger.LogInformation("Non-admin user (Role: {Role}) warehouse-filtered: returning placeholder empty list", role);
                entities = Enumerable.Empty<Transaction>();
            }

            var count = entities.Count();
            _logger.LogInformation("Warehouse-filtered retrieval returned {TransactionCount} transactions", count);
            return entities.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse-filtered transactions for role {Role}, warehouse {WarehouseId}",
                role, warehouseId);
            throw;
        }
    }

    /// <summary>
    /// HIERARCHY: Retrieves all transactions visible to the user across their warehouse tree(s).
    /// Resolves accessible warehouse node IDs from the DB (ownership/assignment), then filters
    /// transactions by their related Item's WarehouseId.
    /// SECURITY: No cross-owner visibility — Admins only see transactions in trees they own.
    /// </summary>
    public async Task<IEnumerable<TransactionDto>> GetAllForUserAsync(int userId, string role, int? userWarehouseId)
    {
        _logger.LogInformation("Retrieving hierarchy-scoped transactions. UserId: {UserId}, Role: {Role}", userId, role);
        try
        {
            var warehouseIds = new List<int>();

            if (role == "Admin")
            {
                // Owner: all owned trees (DB is the source of truth)
                var owned = await _warehouseRepository.GetByOwnerAsync(userId);
                foreach (var main in owned)
                {
                    warehouseIds.Add(main.Id);
                    warehouseIds.AddRange(main.SubWarehouses.Select(s => s.Id));
                }
            }
            else if ((role == "Employee" || role == "ShiftManager") && userWarehouseId.HasValue)
            {
                // Employee: assigned main warehouse + its sub-warehouses
                warehouseIds.Add(userWarehouseId.Value);
                var subs = await _warehouseRepository.GetSubWarehousesAsync(userWarehouseId.Value);
                warehouseIds.AddRange(subs.Select(s => s.Id));
            }

            if (warehouseIds.Count == 0)
            {
                _logger.LogWarning("User {UserId} (Role: {Role}) has no accessible warehouses for transactions", userId, role);
                return Enumerable.Empty<TransactionDto>();
            }

            var entities = await _transactionRepository.GetAllAsync();
            var filtered = entities.Where(t => t.Item != null && warehouseIds.Contains(t.Item.WarehouseId)).ToList();

            _logger.LogInformation("Hierarchy-scoped retrieval returned {TransactionCount} transactions across {WarehouseCount} warehouse nodes",
                filtered.Count, warehouseIds.Count);
            return filtered.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving hierarchy-scoped transactions for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    public async Task<TransactionDto> CreateAsync(CreateTransactionDto dto)
    {
        _logger.LogInformation(
            "Creating transaction. ItemID: {ItemId}, Type: {TransactionType}, Quantity: {Quantity}, Notes: {Notes}",
            dto.ItemId, dto.Type, dto.Quantity, dto.Notes ?? "N/A");
        try
        {
            // Step 1: Validate item exists
            var item = await _itemRepository.GetByIdAsync(dto.ItemId);
            if (item == null)
            {
                _logger.LogWarning("Cannot create transaction - item not found. ItemID: {ItemId}", dto.ItemId);
                throw new KeyNotFoundException($"Item with ID {dto.ItemId} not found");
            }

            // Step 2: Validate stock availability for negative movements
            var quantityAfterTransaction = item.Quantity + dto.Quantity;
            if (quantityAfterTransaction < 0)
            {
                _logger.LogWarning(
                    "Stock validation failed. ItemID: {ItemId}, CurrentQuantity: {CurrentQuantity}, " +
                    "RequestedQuantity: {RequestedQuantity}, ResultingQuantity: {ResultingQuantity}",
                    dto.ItemId, item.Quantity, dto.Quantity, quantityAfterTransaction);
                throw new InvalidOperationException(
                    $"Insufficient stock. Current quantity: {item.Quantity}, requested: {dto.Quantity}");
            }

            // Step 3: Create transaction record
            var entity = new Transaction
            {
                ItemId = dto.ItemId,
                Type = dto.Type,
                Quantity = dto.Quantity,
                Notes = dto.Notes
            };

            var created = await _transactionRepository.CreateAsync(entity);
            _logger.LogInformation("Transaction created. ID: {TransactionId}, ItemID: {ItemId}", created.Id, dto.ItemId);

            // Step 4: Update item quantity atomically
            item.Quantity += dto.Quantity;
            item.UpdatedAt = DateTime.UtcNow;
            await _itemRepository.UpdateAsync(item);

            _logger.LogInformation(
                "Item quantity updated. ItemID: {ItemId}, Type: {TransactionType}, " +
                "OldQuantity: {OldQuantity}, NewQuantity: {NewQuantity}",
                dto.ItemId, dto.Type, item.Quantity - dto.Quantity, item.Quantity);

            return MapToDto(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transaction for item ID: {ItemId}, Type: {TransactionType}",
                dto.ItemId, dto.Type);
            throw;
        }
    }

    /// <summary>
    /// Maps Transaction entity to TransactionDto for API responses.
    /// </summary>
    private static TransactionDto MapToDto(Transaction entity)
    {
        return new TransactionDto
        {
            Id = entity.Id,
            ItemId = entity.ItemId,
            Type = entity.Type,
            Quantity = entity.Quantity,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
            ProductSku = entity.Item?.ProductCatalog?.Sku,
            ProductName = entity.Item?.ProductCatalog?.Name
        };
    }
}
