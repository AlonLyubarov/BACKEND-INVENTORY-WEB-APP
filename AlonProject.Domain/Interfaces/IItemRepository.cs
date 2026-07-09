using AlonProject.Domain.Entities;

namespace AlonProject.Domain.Interfaces;

public interface IItemRepository
{
    // Basic operations
    Task<Item?> GetByIdAsync(int id);
    Task<IEnumerable<Item>> GetByProductCatalogIdAsync(int productCatalogId);
    Task<IEnumerable<Item>> GetByLocationAsync(string location);
    Task<IEnumerable<Item>> GetAllAsync();
    Task<Item> CreateAsync(Item entity);
    Task<Item> UpdateAsync(Item entity);
    Task<bool> DeleteAsync(int id);

    // SECURITY: Warehouse-scoped operations for multi-tenant data isolation
    Task<Item?> GetByIdAsync(int id, int warehouseId);  // Get item by ID from specific warehouse
    Task<IEnumerable<Item>> GetByProductCatalogIdAsync(int productCatalogId, int warehouseId);  // Products in warehouse
    Task<IEnumerable<Item>> GetByLocationAsync(string location, int warehouseId);  // Items by location in warehouse
    Task<IEnumerable<Item>> GetAllAsync(int warehouseId);  // All items in warehouse

    // HIERARCHY: Query items across a set of warehouse nodes (main + its sub-warehouses)
    Task<IEnumerable<Item>> GetByWarehouseIdsAsync(IEnumerable<int> warehouseIds);
}
