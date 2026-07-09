using AlonProject.Application.DTOs;

namespace AlonProject.Application.Interfaces;

/// <summary>
/// Service for warehouse management and inventory aggregation.
/// Provides comprehensive warehouse information including items, transactions, and statistics.
/// </summary>
public interface IWarehouseService
{
    /// <summary>
    /// Retrieves a warehouse by ID.
    /// </summary>
    /// <param name="id">The warehouse ID.</param>
    /// <returns>Warehouse details or null if not found.</returns>
    Task<WarehouseDto?> GetByIdAsync(int id);

    /// <summary>
    /// Retrieves all warehouses.
    /// </summary>
    /// <returns>Collection of all warehouses.</returns>
    Task<IEnumerable<WarehouseDto>> GetAllAsync();

    /// <summary>
    /// Retrieves all MAIN warehouses owned by the given user, including their sub-warehouses.
    /// </summary>
    /// <param name="ownerId">Owner (Admin) user ID.</param>
    /// <returns>Owned main warehouses with SubWarehouses populated.</returns>
    Task<IEnumerable<WarehouseDto>> GetByOwnerAsync(int ownerId);

    /// <summary>
    /// Retrieves the direct sub-warehouses of a node.
    /// </summary>
    /// <param name="parentId">Parent warehouse ID.</param>
    /// <returns>Collection of sub-warehouse DTOs.</returns>
    Task<IEnumerable<WarehouseDto>> GetSubWarehousesAsync(int parentId);

    /// <summary>
    /// Creates a new SUB-warehouse under the given parent.
    /// Validates the parent exists and is itself a MAIN warehouse (one nesting level only).
    /// </summary>
    /// <param name="parentId">Parent (main) warehouse ID.</param>
    /// <param name="dto">Sub-warehouse creation data (name, location).</param>
    /// <returns>The created sub-warehouse.</returns>
    /// <exception cref="KeyNotFoundException">Parent warehouse not found.</exception>
    /// <exception cref="InvalidOperationException">Parent is itself a sub-warehouse (nesting rejected).</exception>
    Task<WarehouseDto> CreateSubWarehouseAsync(int parentId, CreateWarehouseDto dto);

    /// <summary>
    /// Retrieves comprehensive warehouse details including:
    /// - Warehouse information
    /// - All items in the warehouse
    /// - All transactions for those items
    /// - Inventory summary statistics
    /// </summary>
    /// <param name="id">The warehouse ID.</param>
    /// <returns>Detailed warehouse information or null if not found.</returns>
    Task<WarehouseDetailsDto?> GetWarehouseDetailsAsync(int id);

    /// <summary>
    /// Retrieves all items in a specific warehouse.
    /// </summary>
    /// <param name="warehouseId">The warehouse ID.</param>
    /// <returns>Collection of items in the warehouse.</returns>
    Task<IEnumerable<ItemDto>> GetWarehouseItemsAsync(int warehouseId);

    /// <summary>
    /// Retrieves all transactions for items in a specific warehouse.
    /// </summary>
    /// <param name="warehouseId">The warehouse ID.</param>
    /// <returns>Collection of transactions for items in the warehouse.</returns>
    Task<IEnumerable<TransactionDto>> GetWarehouseTransactionsAsync(int warehouseId);

    /// <summary>
    /// Creates a new MAIN warehouse owned by the given user.
    /// </summary>
    /// <param name="dto">Warehouse creation data (name, location).</param>
    /// <param name="ownerId">The Admin user who will own this warehouse.</param>
    /// <returns>The created warehouse with server-calculated fields.</returns>
    Task<WarehouseDto> CreateAsync(CreateWarehouseDto dto, int ownerId);

    /// <summary>
    /// Updates an existing warehouse.
    /// </summary>
    /// <param name="id">The warehouse ID.</param>
    /// <param name="dto">Updated warehouse data (name, location).</param>
    /// <returns>The updated warehouse or null if not found.</returns>
    Task<WarehouseDto?> UpdateAsync(int id, CreateWarehouseDto dto);

    /// <summary>
    /// Deletes a warehouse.
    /// </summary>
    /// <param name="id">The warehouse ID.</param>
    /// <returns>True if deleted; false if not found.</returns>
    Task<bool> DeleteAsync(int id);
}
