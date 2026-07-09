using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using AlonProject.Domain.Entities;
using AlonProject.Domain.Enums;
using AlonProject.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AlonProject.Application.Services;

/// <summary>
/// Service for warehouse management and inventory aggregation.
/// Provides comprehensive warehouse information including items, transactions, and statistics.
/// </summary>
public class WarehouseService : IWarehouseService
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IItemRepository _itemRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<WarehouseService> _logger;

    public WarehouseService(
        IWarehouseRepository warehouseRepository,
        IItemRepository itemRepository,
        ITransactionRepository transactionRepository,
        IUserRepository userRepository,
        ILogger<WarehouseService> logger)
    {
        _warehouseRepository = warehouseRepository;
        _itemRepository = itemRepository;
        _transactionRepository = transactionRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a warehouse by ID.
    /// </summary>
    public async Task<WarehouseDto?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Retrieving warehouse with ID: {WarehouseId}", id);
        try
        {
            var entity = await _warehouseRepository.GetByIdAsync(id);
            if (entity == null)
            {
                _logger.LogWarning("Warehouse not found. ID: {WarehouseId}", id);
                return null;
            }

            var dto = await MapToDtoAsync(entity);
            _logger.LogDebug("Warehouse retrieved successfully. ID: {WarehouseId}, Name: {WarehouseName}", id, entity.Name);
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse with ID: {WarehouseId}", id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all warehouses.
    /// </summary>
    public async Task<IEnumerable<WarehouseDto>> GetAllAsync()
    {
        _logger.LogInformation("Retrieving all warehouses");
        try
        {
            var entities = await _warehouseRepository.GetAllAsync();
            var count = entities.Count();
            _logger.LogInformation("Retrieved {WarehouseCount} warehouses", count);

            var dtos = new List<WarehouseDto>();
            foreach (var entity in entities)
            {
                dtos.Add(await MapToDtoAsync(entity));
            }
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all warehouses");
            throw;
        }
    }

    /// <summary>
    /// Retrieves all MAIN warehouses owned by the given user, including their sub-warehouses.
    /// </summary>
    public async Task<IEnumerable<WarehouseDto>> GetByOwnerAsync(int ownerId)
    {
        _logger.LogInformation("Retrieving warehouses owned by user: {OwnerId}", ownerId);
        try
        {
            var entities = await _warehouseRepository.GetByOwnerAsync(ownerId);
            var dtos = new List<WarehouseDto>();
            foreach (var entity in entities)
            {
                dtos.Add(await MapToDtoAsync(entity));
            }
            _logger.LogInformation("Retrieved {WarehouseCount} owned warehouses for user {OwnerId}", dtos.Count, ownerId);
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouses for owner: {OwnerId}", ownerId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the direct sub-warehouses of a node.
    /// </summary>
    public async Task<IEnumerable<WarehouseDto>> GetSubWarehousesAsync(int parentId)
    {
        _logger.LogInformation("Retrieving sub-warehouses of warehouse: {ParentId}", parentId);
        try
        {
            var entities = await _warehouseRepository.GetSubWarehousesAsync(parentId);
            var dtos = new List<WarehouseDto>();
            foreach (var entity in entities)
            {
                dtos.Add(await MapToDtoAsync(entity));
            }
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sub-warehouses of: {ParentId}", parentId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new SUB-warehouse under the given parent.
    /// Rejects nesting under another sub-warehouse (one level only).
    /// </summary>
    public async Task<WarehouseDto> CreateSubWarehouseAsync(int parentId, CreateWarehouseDto dto)
    {
        _logger.LogInformation("Creating sub-warehouse under parent {ParentId}. Name: {Name}", parentId, dto.Name);

        var parent = await _warehouseRepository.GetByIdAsync(parentId);
        if (parent == null)
        {
            throw new KeyNotFoundException($"Parent warehouse {parentId} not found.");
        }

        if (parent.ParentWarehouseId != null)
        {
            // One nesting level only: cannot create a sub-warehouse under another sub-warehouse
            throw new InvalidOperationException("Cannot create a sub-warehouse under another sub-warehouse. Only main warehouses can have sub-warehouses.");
        }

        var entity = new Warehouse
        {
            Name = dto.Name,
            Location = dto.Location,
            ParentWarehouseId = parentId,
            OwnerId = null,  // Ownership is inherited from the parent (root) warehouse
            CreatedAt = DateTime.UtcNow
        };

        var created = await _warehouseRepository.CreateAsync(entity);
        _logger.LogInformation("Sub-warehouse created. ID: {WarehouseId}, Parent: {ParentId}", created.Id, parentId);

        return await MapToDtoAsync(created);
    }

    /// <summary>
    /// Retrieves comprehensive warehouse details including items, transactions, and statistics.
    /// </summary>
    public async Task<WarehouseDetailsDto?> GetWarehouseDetailsAsync(int id)
    {
        _logger.LogInformation("Retrieving warehouse details for ID: {WarehouseId}", id);
        try
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(id);
            if (warehouse == null)
            {
                _logger.LogWarning("Warehouse not found for details. ID: {WarehouseId}", id);
                return null;
            }

            // Get items in this warehouse node AND all its sub-warehouses (hierarchy-aware, FK-based)
            var nodeIds = await GetNodeAndSubIdsAsync(id);
            var items = await _itemRepository.GetByWarehouseIdsAsync(nodeIds);
            var itemDtos = new List<ItemDto>();
            foreach (var item in items)
            {
                itemDtos.Add(await MapItemToDtoAsync(item));
            }

            // Get all transactions for this warehouse tree. Filter by the item's
            // warehouse node (not by the visible items list) so the audit trail of
            // soft-deleted items keeps showing what was removed.
            var allTransactions = await _transactionRepository.GetAllAsync();
            var warehouseTransactions = allTransactions
                .Where(t => t.Item != null && nodeIds.Contains(t.Item.WarehouseId))
                .ToList();

            var transactionDtos = warehouseTransactions.Select(MapTransactionToDto).ToList();

            // Calculate inventory summary
            var summary = new InventorySummaryDto
            {
                TotalItems = itemDtos.Count(),
                TotalQuantity = itemDtos.Sum(i => i.Quantity),
                LowStockItems = itemDtos.Count(i => i.Quantity > 0 && i.Quantity <= i.MinimumStockLevel),
                OutOfStockItems = itemDtos.Count(i => i.Quantity == 0),
                TransactionCount = transactionDtos.Count()
            };

            var details = new WarehouseDetailsDto
            {
                Id = warehouse.Id,
                Name = warehouse.Name,
                Location = warehouse.Location,
                CreatedAt = warehouse.CreatedAt,
                UpdatedAt = warehouse.UpdatedAt,
                UserCount = await GetUserCountAsync(warehouse),
                Items = itemDtos,
                Transactions = transactionDtos,
                InventorySummary = summary
            };

            _logger.LogInformation(
                "Warehouse details retrieved. ID: {WarehouseId}, Items: {ItemCount}, Transactions: {TransactionCount}",
                id, itemDtos.Count, transactionDtos.Count);

            return details;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse details for ID: {WarehouseId}", id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all items in a warehouse node and its sub-warehouses.
    /// </summary>
    public async Task<IEnumerable<ItemDto>> GetWarehouseItemsAsync(int warehouseId)
    {
        _logger.LogInformation("Retrieving items for warehouse ID: {WarehouseId}", warehouseId);
        try
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId);
            if (warehouse == null)
            {
                _logger.LogWarning("Warehouse not found. ID: {WarehouseId}", warehouseId);
                return Enumerable.Empty<ItemDto>();
            }

            // Hierarchy-aware: include items of sub-warehouses (FK-based, not name-based)
            var nodeIds = await GetNodeAndSubIdsAsync(warehouseId);
            var items = await _itemRepository.GetByWarehouseIdsAsync(nodeIds);
            var dtos = new List<ItemDto>();
            foreach (var item in items)
            {
                dtos.Add(await MapItemToDtoAsync(item));
            }

            _logger.LogInformation("Retrieved {ItemCount} items for warehouse ID: {WarehouseId} (including sub-warehouses)", dtos.Count, warehouseId);
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items for warehouse ID: {WarehouseId}", warehouseId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all transactions for items in a specific warehouse.
    /// </summary>
    public async Task<IEnumerable<TransactionDto>> GetWarehouseTransactionsAsync(int warehouseId)
    {
        _logger.LogInformation("Retrieving transactions for warehouse ID: {WarehouseId}", warehouseId);
        try
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId);
            if (warehouse == null)
            {
                _logger.LogWarning("Warehouse not found. ID: {WarehouseId}", warehouseId);
                return Enumerable.Empty<TransactionDto>();
            }

            // Hierarchy-aware: filter by the item's warehouse node (not by the
            // visible items list) so soft-deleted items keep their audit trail.
            var nodeIds = await GetNodeAndSubIdsAsync(warehouseId);
            var allTransactions = await _transactionRepository.GetAllAsync();
            var warehouseTransactions = allTransactions
                .Where(t => t.Item != null && nodeIds.Contains(t.Item.WarehouseId))
                .OrderByDescending(t => t.CreatedAt)
                .ToList();

            var dtos = warehouseTransactions.Select(MapTransactionToDto).ToList();

            _logger.LogInformation(
                "Retrieved {TransactionCount} transactions for warehouse ID: {WarehouseId}",
                dtos.Count, warehouseId);

            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transactions for warehouse ID: {WarehouseId}", warehouseId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new MAIN warehouse owned by the given user.
    /// </summary>
    public async Task<WarehouseDto> CreateAsync(CreateWarehouseDto dto, int ownerId)
    {
        _logger.LogInformation("Creating new main warehouse. Name: {WarehouseName}, Location: {Location}, Owner: {OwnerId}", dto.Name, dto.Location, ownerId);
        try
        {
            var entity = new Warehouse
            {
                Name = dto.Name,
                Location = dto.Location,
                OwnerId = ownerId,
                ParentWarehouseId = null,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _warehouseRepository.CreateAsync(entity);
            _logger.LogInformation("Main warehouse created successfully. ID: {WarehouseId}, Name: {WarehouseName}, Owner: {OwnerId}", created.Id, dto.Name, ownerId);

            return await MapToDtoAsync(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating warehouse. Name: {WarehouseName}", dto.Name);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing warehouse.
    /// </summary>
    public async Task<WarehouseDto?> UpdateAsync(int id, CreateWarehouseDto dto)
    {
        _logger.LogInformation("Updating warehouse. ID: {WarehouseId}, Name: {WarehouseName}", id, dto.Name);
        try
        {
            var entity = await _warehouseRepository.GetByIdAsync(id);
            if (entity == null)
            {
                _logger.LogWarning("Warehouse not found for update. ID: {WarehouseId}", id);
                return null;
            }

            entity.Name = dto.Name;
            entity.Location = dto.Location;
            entity.UpdatedAt = DateTime.UtcNow;

            // Update in database
            await _warehouseRepository.UpdateAsync(entity);

            _logger.LogInformation("Warehouse updated successfully. ID: {WarehouseId}", id);
            return await MapToDtoAsync(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating warehouse. ID: {WarehouseId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a warehouse.
    /// Rejects deletion when the warehouse still has sub-warehouses (Restrict FK):
    /// they must be deleted or moved first.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        _logger.LogInformation("Deleting warehouse. ID: {WarehouseId}", id);
        try
        {
            var subs = await _warehouseRepository.GetSubWarehousesAsync(id);
            if (subs.Any())
            {
                _logger.LogWarning("Cannot delete warehouse {WarehouseId}: it has {SubCount} sub-warehouses", id, subs.Count());
                throw new InvalidOperationException("Cannot delete a warehouse that has sub-warehouses. Delete or move its sub-warehouses first.");
            }

            // Users FK is Restrict — fail with a clear message instead of a raw DB error
            var assignedUsers = await _userRepository.GetByWarehouseIdAsync(id);
            if (assignedUsers.Any())
            {
                _logger.LogWarning("Cannot delete warehouse {WarehouseId}: it has {UserCount} assigned users", id, assignedUsers.Count());
                throw new InvalidOperationException("Cannot delete a warehouse that has team members assigned to it. Remove its users first (Users tab).");
            }

            var deleted = await _warehouseRepository.DeleteAsync(id);
            if (!deleted)
            {
                _logger.LogWarning("Warehouse not found for deletion. ID: {WarehouseId}", id);
                return false;
            }

            _logger.LogInformation("Warehouse deleted successfully. ID: {WarehouseId}", id);
            return true;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting warehouse. ID: {WarehouseId}", id);
            throw;
        }
    }

    /// <summary>
    /// Maps Warehouse entity to WarehouseDto, including hierarchy fields.
    /// </summary>
    private async Task<WarehouseDto> MapToDtoAsync(Warehouse entity)
    {
        // Load sub-warehouses if not already included
        var subs = entity.SubWarehouses?.Count > 0
            ? entity.SubWarehouses
            : (await _warehouseRepository.GetSubWarehousesAsync(entity.Id)).ToList();

        return new WarehouseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Location = entity.Location,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            UserCount = await GetUserCountAsync(entity),
            ParentWarehouseId = entity.ParentWarehouseId,
            SubWarehouses = subs.Select(s => new SubWarehouseDto
            {
                Id = s.Id,
                Name = s.Name,
                Location = s.Location
            }).ToList()
        };
    }

    /// <summary>
    /// Counts the users assigned to a warehouse. The Users navigation property
    /// is not eager-loaded by the repository queries, so counting through the
    /// user repository is the reliable path; the navigation is used when present.
    /// </summary>
    private async Task<int> GetUserCountAsync(Warehouse entity)
    {
        if (entity.Users != null && entity.Users.Count > 0)
        {
            return entity.Users.Count;
        }

        var users = await _userRepository.GetByWarehouseIdAsync(entity.Id);
        return users.Count();
    }

    /// <summary>
    /// Collects the IDs of a warehouse node and all its direct sub-warehouses.
    /// Used to scope item/transaction queries to the full hierarchy.
    /// </summary>
    private async Task<List<int>> GetNodeAndSubIdsAsync(int warehouseId)
    {
        var ids = new List<int> { warehouseId };
        var subs = await _warehouseRepository.GetSubWarehousesAsync(warehouseId);
        ids.AddRange(subs.Select(s => s.Id));
        return ids;
    }

    /// <summary>
    /// Maps Item entity to ItemDto.
    /// </summary>
    private async Task<ItemDto> MapItemToDtoAsync(Item entity)
    {
        string status = entity.Quantity == 0 ? "Out of Stock"
            : entity.Quantity <= entity.MinimumStockLevel ? "Low"
            : "In Stock";

        var catalogDto = new ProductCatalogDto();
        if (entity.ProductCatalog != null)
        {
            catalogDto = new ProductCatalogDto
            {
                Id = entity.ProductCatalog.Id,
                Sku = entity.ProductCatalog.Sku,
                Name = entity.ProductCatalog.Name,
                Price = entity.ProductCatalog.Price,
                Barcode = entity.ProductCatalog.Barcode,
                CreatedAt = entity.ProductCatalog.CreatedAt,
                UpdatedAt = entity.ProductCatalog.UpdatedAt
            };
        }

        return new ItemDto
        {
            Id = entity.Id,
            ProductCatalogId = entity.ProductCatalogId,
            WarehouseId = entity.WarehouseId,
            Location = entity.Location,
            Quantity = entity.Quantity,
            MinimumStockLevel = entity.MinimumStockLevel,
            Status = status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            ProductCatalog = catalogDto
        };
    }

    /// <summary>
    /// Maps Transaction entity to TransactionDto.
    /// </summary>
    private TransactionDto MapTransactionToDto(Transaction entity)
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
