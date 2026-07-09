using AlonProject.Domain.Entities;

namespace AlonProject.Domain.Interfaces;

/// <summary>
/// Repository interface for Warehouse entity operations.
/// Defines contract for data access to Warehouse records.
/// </summary>
public interface IWarehouseRepository
{
    /// <summary>
    /// Retrieves a warehouse by its ID.
    /// </summary>
    /// <param name="id">The warehouse ID.</param>
    /// <returns>The warehouse if found; otherwise null.</returns>
    Task<Warehouse?> GetByIdAsync(int id);

    /// <summary>
    /// Retrieves all warehouses.
    /// </summary>
    /// <returns>Collection of all warehouses.</returns>
    Task<IEnumerable<Warehouse>> GetAllAsync();

    /// <summary>
    /// Retrieves the first warehouse (useful for getting the default).
    /// </summary>
    /// <returns>The first warehouse if any exist; otherwise null.</returns>
    Task<Warehouse?> GetFirstAsync();

    /// <summary>
    /// Creates a new warehouse.
    /// </summary>
    /// <param name="entity">The warehouse entity to create.</param>
    /// <returns>The created warehouse with assigned ID.</returns>
    Task<Warehouse> CreateAsync(Warehouse entity);

    /// <summary>
    /// Updates an existing warehouse.
    /// </summary>
    /// <param name="entity">The warehouse entity to update.</param>
    /// <returns>The updated warehouse.</returns>
    Task<Warehouse> UpdateAsync(Warehouse entity);

    /// <summary>
    /// Deletes a warehouse by ID.
    /// </summary>
    /// <param name="id">The warehouse ID to delete.</param>
    /// <returns>True if the warehouse was deleted; false if not found.</returns>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks whether the given user owns the ROOT (main) warehouse of the specified node.
    /// Works for both main warehouses (checks OwnerId directly) and sub-warehouses
    /// (resolves the parent chain first).
    /// </summary>
    /// <param name="warehouseId">Any warehouse node (main or sub).</param>
    /// <param name="userId">The user to check ownership for.</param>
    /// <returns>True if userId owns the root of the node; false otherwise (including missing warehouse).</returns>
    Task<bool> IsOwnerAsync(int warehouseId, int userId);

    /// <summary>
    /// Retrieves all MAIN warehouses owned by the specified user.
    /// Sub-warehouses are included via the SubWarehouses navigation.
    /// </summary>
    /// <param name="ownerId">The owner (Admin) user ID.</param>
    /// <returns>Main warehouses (ParentWarehouseId == null) owned by the user.</returns>
    Task<IEnumerable<Warehouse>> GetByOwnerAsync(int ownerId);

    /// <summary>
    /// Retrieves the direct sub-warehouses of the specified parent warehouse.
    /// </summary>
    /// <param name="parentId">The parent warehouse ID.</param>
    /// <returns>Collection of sub-warehouses (possibly empty).</returns>
    Task<IEnumerable<Warehouse>> GetSubWarehousesAsync(int parentId);

    /// <summary>
    /// Resolves the ROOT (main) warehouse of any node by walking ParentWarehouseId until null.
    /// With one nesting level this is a single hop.
    /// </summary>
    /// <param name="warehouseId">Any warehouse node (main or sub).</param>
    /// <returns>The root warehouse, or null if the node does not exist.</returns>
    Task<Warehouse?> GetRootWarehouseAsync(int warehouseId);
}
