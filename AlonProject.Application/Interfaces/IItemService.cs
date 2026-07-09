using AlonProject.Application.DTOs;

namespace AlonProject.Application.Interfaces;

public interface IItemService
{
    Task<ItemDto?> GetByIdAsync(int id);
    Task<IEnumerable<ItemDto>> GetByProductCatalogIdAsync(int productCatalogId);
    Task<IEnumerable<ItemDto>> GetByLocationAsync(string location);
    Task<IEnumerable<ItemDto>> GetAllAsync();

    /// <summary>
    /// Retrieves all items filtered by user role and warehouse.
    /// Security boundary: Admin sees their warehouse items; Employee/ShiftManager see only their warehouse's items.
    /// Parameters: role (from JWT ClaimTypes.Role), warehouseId (from JWT custom "WarehouseId" claim).
    /// </summary>
    Task<IEnumerable<ItemDto>> GetAllAsync(string role, int? warehouseId);

    /// <summary>
    /// HIERARCHY: Retrieves all items visible to the user across their warehouse tree(s).
    /// Admin (owner): items from ALL owned main warehouses and their sub-warehouses (DB-resolved).
    /// Employee/ShiftManager: items from their assigned main warehouse and its sub-warehouses.
    /// </summary>
    Task<IEnumerable<ItemDto>> GetAllForUserAsync(int userId, string role, int? userWarehouseId);

    /// <summary>
    /// HIERARCHY: Retrieves items for a product catalog entry, scoped to the user's accessible warehouse tree(s).
    /// </summary>
    Task<IEnumerable<ItemDto>> GetByProductCatalogIdForUserAsync(int productCatalogId, int userId, string role, int? userWarehouseId);

    /// <summary>
    /// HIERARCHY: Retrieves items at a location, scoped to the user's accessible warehouse tree(s).
    /// </summary>
    Task<IEnumerable<ItemDto>> GetByLocationForUserAsync(string location, int userId, string role, int? userWarehouseId);

    // SECURITY: Warehouse-scoped operations for multi-tenant isolation
    Task<ItemDto?> GetByIdAsync(int id, int warehouseId);  // Get item by ID from specific warehouse
    Task<IEnumerable<ItemDto>> GetByProductCatalogIdAsync(int productCatalogId, int warehouseId);  // Products in warehouse
    Task<IEnumerable<ItemDto>> GetByLocationAsync(string location, int warehouseId);  // Items by location in warehouse

    Task<ItemDto> CreateAsync(CreateItemDto dto);
    Task<ItemDto> CreateAsync(CreateItemDto dto, int warehouseId);  // With warehouse assignment
    Task<ItemDto> UpdateAsync(int id, CreateItemDto dto);
    Task<bool> DeleteAsync(int id);
}
