using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AlonProject.Application.Services;

/// <summary>
/// Service for managing inventory items with stock tracking and validation.
/// Handles item operations linked to products with location-based quantity management.
/// </summary>
public class ItemService : IItemService
{
    private readonly IItemRepository _repository;
    private readonly IProductCatalogRepository _productCatalogRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ILogger<ItemService> _logger;

    public ItemService(
        IItemRepository repository,
        IProductCatalogRepository productCatalogRepository,
        IWarehouseRepository warehouseRepository,
        ILogger<ItemService> logger)
    {
        _repository = repository;
        _productCatalogRepository = productCatalogRepository;
        _warehouseRepository = warehouseRepository;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves an item by its unique ID with product details.
    /// </summary>
    public async Task<ItemDto?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Retrieving item with ID: {ItemId}", id);
        try
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
            {
                _logger.LogWarning("Item not found. ID: {ItemId}", id);
                return null;
            }
            _logger.LogDebug("Item retrieved successfully. ID: {ItemId}, Location: {Location}, Quantity: {Quantity}",
                id, entity.Location, entity.Quantity);
            return await MapToDtoAsync(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving item with ID: {ItemId}", id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all items for a specific product.
    /// </summary>
    public async Task<IEnumerable<ItemDto>> GetByProductCatalogIdAsync(int productCatalogId)
    {
        _logger.LogInformation("Retrieving items for product catalog. ProductID: {ProductId}", productCatalogId);
        try
        {
            var entities = await _repository.GetByProductCatalogIdAsync(productCatalogId);
            var count = entities.Count();
            _logger.LogInformation("Retrieved {ItemCount} items for product {ProductId}", count, productCatalogId);
            var dtos = new List<ItemDto>();
            foreach (var entity in entities)
            {
                dtos.Add(await MapToDtoAsync(entity));
            }
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items for product ID: {ProductId}", productCatalogId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all items stored in a specific location.
    /// </summary>
    public async Task<IEnumerable<ItemDto>> GetByLocationAsync(string location)
    {
        _logger.LogInformation("Retrieving items by location: {Location}", location);
        try
        {
            var entities = await _repository.GetByLocationAsync(location);
            var count = entities.Count();
            _logger.LogInformation("Retrieved {ItemCount} items from location: {Location}", count, location);
            var dtos = new List<ItemDto>();
            foreach (var entity in entities)
            {
                dtos.Add(await MapToDtoAsync(entity));
            }
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items by location: {Location}", location);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all items from inventory.
    /// </summary>
    public async Task<IEnumerable<ItemDto>> GetAllAsync()
    {
        _logger.LogInformation("Retrieving all inventory items");
        try
        {
            var entities = await _repository.GetAllAsync();
            var count = entities.Count();
            _logger.LogInformation("Retrieved {ItemCount} items from inventory", count);
            var dtos = new List<ItemDto>();
            foreach (var entity in entities)
            {
                dtos.Add(await MapToDtoAsync(entity));
            }
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all inventory items");
            throw;
        }
    }

    /// <summary>
    /// Retrieves all items filtered by user role and warehouse (security boundary).
    /// Admin users see all items; Employee/ShiftManager users see only their warehouse's items.
    /// 
    /// SECURITY NOTE: This method implements warehouse-based data isolation.
    /// The current Item entity schema does not have a direct WarehouseId property.
    /// <summary>
    /// Retrieves all items, with warehouse-scoped filtering based on user role and warehouse assignment.
    /// SECURITY: Admins see only their warehouse; Non-Admins get empty list (no cross-warehouse visibility).
    /// </summary>
    public async Task<IEnumerable<ItemDto>> GetAllAsync(string role, int? warehouseId)
    {
        _logger.LogInformation("Retrieving all items with warehouse filtering. Role: {Role}, WarehouseId: {WarehouseId}",
            role, warehouseId ?? 0);
        try
        {
            IEnumerable<Item> entities;

            // SECURITY: Filter items based on role and warehouse
            if (role == "Admin")
            {
                if (warehouseId.HasValue)
                {
                    // Admin with warehouse: see only their warehouse items
                    entities = await _repository.GetAllAsync(warehouseId.Value);
                    _logger.LogDebug("Admin user with warehouse: returning {ItemCount} items from warehouse {WarehouseId}", 
                        entities.Count(), warehouseId);
                }
                else
                {
                    // Admin without warehouse: see nothing (not assigned yet)
                    _logger.LogWarning("Admin user without warehouse assignment attempting to view items");
                    entities = Enumerable.Empty<Item>();
                }
            }
            else if (role == "Employee" || role == "ShiftManager")
            {
                if (warehouseId.HasValue)
                {
                    // Employee/ShiftManager: see only their warehouse items
                    entities = await _repository.GetAllAsync(warehouseId.Value);
                    _logger.LogDebug("Non-admin user (Role: {Role}): returning {ItemCount} items from warehouse {WarehouseId}", 
                        role, entities.Count(), warehouseId);
                }
                else
                {
                    // Non-admin without warehouse: see nothing
                    _logger.LogWarning("Non-admin user without warehouse assignment attempting to view items");
                    entities = Enumerable.Empty<Item>();
                }
            }
            else
            {
                // Unknown role: secure default is empty
                _logger.LogWarning("Unknown role attempting to view items: {Role}", role);
                entities = Enumerable.Empty<Item>();
            }

            var count = entities.Count();
            _logger.LogInformation("Warehouse-filtered retrieval returned {ItemCount} items", count);
            var dtos = new List<ItemDto>();
            foreach (var entity in entities)
            {
                dtos.Add(await MapToDtoAsync(entity));
            }
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse-filtered items for role {Role}, warehouse {WarehouseId}",
                role, warehouseId);
            throw;
        }
    }

    /// <summary>
    /// HIERARCHY: Resolves the set of warehouse node IDs the user may see.
    /// Admin (owner): every owned main warehouse + its sub-warehouses (DB-resolved via Warehouse.OwnerId).
    /// Employee/ShiftManager: their assigned main warehouse + its sub-warehouses.
    /// Unknown role or no assignment: empty set (secure default).
    /// </summary>
    private async Task<List<int>> GetAccessibleWarehouseIdsAsync(int userId, string role, int? userWarehouseId)
    {
        var ids = new List<int>();

        if (role == "Admin")
        {
            // Owner: all owned trees (DB is the source of truth, not claims)
            var owned = await _warehouseRepository.GetByOwnerAsync(userId);
            foreach (var main in owned)
            {
                ids.Add(main.Id);
                ids.AddRange(main.SubWarehouses.Select(s => s.Id));
            }
        }
        else if ((role == "Employee" || role == "ShiftManager") && userWarehouseId.HasValue)
        {
            // Employee: assigned main warehouse + its sub-warehouses
            ids.Add(userWarehouseId.Value);
            var subs = await _warehouseRepository.GetSubWarehousesAsync(userWarehouseId.Value);
            ids.AddRange(subs.Select(s => s.Id));
        }

        return ids;
    }

    /// <summary>
    /// HIERARCHY: Retrieves all items visible to the user across their warehouse tree(s).
    /// </summary>
    public async Task<IEnumerable<ItemDto>> GetAllForUserAsync(int userId, string role, int? userWarehouseId)
    {
        _logger.LogInformation("Retrieving hierarchy-scoped items. UserId: {UserId}, Role: {Role}", userId, role);
        try
        {
            var warehouseIds = await GetAccessibleWarehouseIdsAsync(userId, role, userWarehouseId);
            if (warehouseIds.Count == 0)
            {
                _logger.LogWarning("User {UserId} (Role: {Role}) has no accessible warehouses", userId, role);
                return Enumerable.Empty<ItemDto>();
            }

            var entities = await _repository.GetByWarehouseIdsAsync(warehouseIds);
            _logger.LogInformation("Hierarchy-scoped retrieval returned {ItemCount} items across {WarehouseCount} warehouse nodes",
                entities.Count(), warehouseIds.Count);

            var dtos = new List<ItemDto>();
            foreach (var entity in entities)
            {
                dtos.Add(await MapToDtoAsync(entity));
            }
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving hierarchy-scoped items for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// HIERARCHY: Retrieves items for a product catalog entry across the user's warehouse tree(s).
    /// </summary>
    public async Task<IEnumerable<ItemDto>> GetByProductCatalogIdForUserAsync(int productCatalogId, int userId, string role, int? userWarehouseId)
    {
        _logger.LogInformation("Retrieving hierarchy-scoped items for product {ProductId}. UserId: {UserId}", productCatalogId, userId);
        var warehouseIds = await GetAccessibleWarehouseIdsAsync(userId, role, userWarehouseId);
        if (warehouseIds.Count == 0)
        {
            return Enumerable.Empty<ItemDto>();
        }

        var entities = await _repository.GetByWarehouseIdsAsync(warehouseIds);
        var filtered = entities.Where(e => e.ProductCatalogId == productCatalogId);

        var dtos = new List<ItemDto>();
        foreach (var entity in filtered)
        {
            dtos.Add(await MapToDtoAsync(entity));
        }
        return dtos;
    }

    /// <summary>
    /// HIERARCHY: Retrieves items at a location across the user's warehouse tree(s).
    /// </summary>
    public async Task<IEnumerable<ItemDto>> GetByLocationForUserAsync(string location, int userId, string role, int? userWarehouseId)
    {
        _logger.LogInformation("Retrieving hierarchy-scoped items at location {Location}. UserId: {UserId}", location, userId);
        var warehouseIds = await GetAccessibleWarehouseIdsAsync(userId, role, userWarehouseId);
        if (warehouseIds.Count == 0)
        {
            return Enumerable.Empty<ItemDto>();
        }

        var entities = await _repository.GetByWarehouseIdsAsync(warehouseIds);
        var filtered = entities.Where(e => e.Location == location);

        var dtos = new List<ItemDto>();
        foreach (var entity in filtered)
        {
            dtos.Add(await MapToDtoAsync(entity));
        }
        return dtos;
    }


    /// <summary>
    /// Creates a new item in inventory after validating product exists.
    /// SECURITY: WarehouseId is required to enforce multi-tenant isolation.
    /// Note: Transaction creation is handled by ItemRepository.CreateAsync
    /// </summary>
    public async Task<ItemDto> CreateAsync(CreateItemDto dto, int warehouseId)
    {
        _logger.LogInformation("Creating new item. ProductID: {ProductId}, Location: {Location}, Quantity: {Quantity}, WarehouseId: {WarehouseId}",
            dto.ProductCatalogId, dto.Location, dto.Quantity, warehouseId);
        try
        {
            // Validate product reference
            var product = await _productCatalogRepository.GetByIdAsync(dto.ProductCatalogId);
            if (product == null)
            {
                _logger.LogWarning("Cannot create item - product not found. ProductID: {ProductId}",
                    dto.ProductCatalogId);
                throw new KeyNotFoundException($"Product with ID {dto.ProductCatalogId} not found");
            }

            var entity = new Item
            {
                WarehouseId = warehouseId,  // SECURITY: Set from authenticated user's warehouse
                ProductCatalogId = dto.ProductCatalogId,
                Location = dto.Location,
                Quantity = dto.Quantity,
                MinimumStockLevel = dto.MinimumStockLevel
            };

            var created = await _repository.CreateAsync(entity);
            _logger.LogInformation(
                "Item created successfully. ID: {ItemId}, ProductID: {ProductId}, Location: {Location}, WarehouseId: {WarehouseId}",
                created.Id, dto.ProductCatalogId, dto.Location, warehouseId);
            return await MapToDtoAsync(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating item for product ID: {ProductId} in warehouse {WarehouseId}", dto.ProductCatalogId, warehouseId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new item in inventory after validating product exists.
    /// Note: Transaction creation is handled by ItemRepository.CreateAsync
    /// DEPRECATED: Use CreateAsync(CreateItemDto, int warehouseId) instead for warehouse isolation.
    /// </summary>
    [Obsolete("Use CreateAsync(CreateItemDto, int warehouseId) to enforce warehouse isolation")]
    public async Task<ItemDto> CreateAsync(CreateItemDto dto)
    {
        _logger.LogWarning("CreateAsync called without warehouseId - using warehouse 0 (DEPRECATED)");
        return await CreateAsync(dto, 0);  // Fallback for backward compatibility
    }

    /// <summary>
    /// Updates item details (location and quantity).
    /// </summary>
    public async Task<ItemDto> UpdateAsync(int id, CreateItemDto dto)
    {
        _logger.LogInformation("Updating item. ID: {ItemId}, Location: {Location}, Quantity: {Quantity}",
            id, dto.Location, dto.Quantity);
        try
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
            {
                _logger.LogWarning("Item not found for update. ID: {ItemId}", id);
                throw new KeyNotFoundException($"Item with ID {id} not found");
            }

            entity.Location = dto.Location;
            entity.Quantity = dto.Quantity;
            entity.UpdatedAt = DateTime.UtcNow;

            var updated = await _repository.UpdateAsync(entity);
            _logger.LogInformation("Item updated successfully. ID: {ItemId}, NewQuantity: {Quantity}", id, dto.Quantity);
            return await MapToDtoAsync(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item with ID: {ItemId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes an item from inventory.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        _logger.LogInformation("Deleting item. ID: {ItemId}", id);
        try
        {
            var result = await _repository.DeleteAsync(id);
            if (result)
            {
                _logger.LogInformation("Item deleted successfully. ID: {ItemId}", id);
            }
            else
            {
                _logger.LogWarning("Item deletion failed - item not found. ID: {ItemId}", id);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item with ID: {ItemId}", id);
            throw;
        }
    }

    /// <summary>
    /// Maps Item entity to ItemDto including related product details and computed stock status.
    /// </summary>
    /// <summary>
    /// SECURITY: Get item by ID, verifying it belongs to the specified warehouse.
    /// </summary>
    public async Task<ItemDto?> GetByIdAsync(int id, int warehouseId)
    {
        _logger.LogInformation("Retrieving item with ID: {ItemId} from warehouse {WarehouseId}", id, warehouseId);
        try
        {
            var entity = await _repository.GetByIdAsync(id, warehouseId);
            if (entity == null)
            {
                _logger.LogWarning("Item not found or not in warehouse. ID: {ItemId}, WarehouseId: {WarehouseId}", id, warehouseId);
                return null;
            }
            _logger.LogDebug("Item retrieved successfully. ID: {ItemId}, WarehouseId: {WarehouseId}", id, warehouseId);
            return await MapToDtoAsync(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving item with ID: {ItemId} from warehouse {WarehouseId}", id, warehouseId);
            throw;
        }
    }

    /// <summary>
    /// SECURITY: Get items by product catalog from specific warehouse.
    /// </summary>
    public async Task<IEnumerable<ItemDto>> GetByProductCatalogIdAsync(int productCatalogId, int warehouseId)
    {
        _logger.LogInformation("Retrieving items for product catalog {ProductId} from warehouse {WarehouseId}", productCatalogId, warehouseId);
        try
        {
            var entities = await _repository.GetByProductCatalogIdAsync(productCatalogId, warehouseId);
            var count = entities.Count();
            _logger.LogInformation("Retrieved {ItemCount} items for product {ProductId} from warehouse {WarehouseId}", count, productCatalogId, warehouseId);
            var dtos = new List<ItemDto>();
            foreach (var entity in entities)
            {
                dtos.Add(await MapToDtoAsync(entity));
            }
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items for product ID: {ProductId} from warehouse {WarehouseId}", productCatalogId, warehouseId);
            throw;
        }
    }

    /// <summary>
    /// SECURITY: Get items by location from specific warehouse.
    /// </summary>
    public async Task<IEnumerable<ItemDto>> GetByLocationAsync(string location, int warehouseId)
    {
        _logger.LogInformation("Retrieving items by location: {Location} from warehouse {WarehouseId}", location, warehouseId);
        try
        {
            var entities = await _repository.GetByLocationAsync(location, warehouseId);
            var count = entities.Count();
            _logger.LogInformation("Retrieved {ItemCount} items from location: {Location} in warehouse {WarehouseId}", count, location, warehouseId);
            var dtos = new List<ItemDto>();
            foreach (var entity in entities)
            {
                dtos.Add(await MapToDtoAsync(entity));
            }
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items by location: {Location} from warehouse {WarehouseId}", location, warehouseId);
            throw;
        }
    }

    private async Task<ItemDto> MapToDtoAsync(Item entity)
    {
        // Calculate stock status based on quantity and minimum threshold
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
            WarehouseId = entity.WarehouseId,  // SECURITY: Include warehouse ID in response
            ProductCatalogId = entity.ProductCatalogId,
            Location = entity.Location,
            Quantity = entity.Quantity,
            MinimumStockLevel = entity.MinimumStockLevel,
            Status = status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            ProductCatalog = catalogDto
        };
    }
}
