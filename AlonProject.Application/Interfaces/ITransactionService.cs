using AlonProject.Application.DTOs;

namespace AlonProject.Application.Interfaces;

public interface ITransactionService
{
    Task<TransactionDto?> GetByIdAsync(int id);
    Task<IEnumerable<TransactionDto>> GetByItemIdAsync(int itemId);
    Task<IEnumerable<TransactionDto>> GetAllAsync();

    /// <summary>
    /// Retrieves all transactions filtered by user role and warehouse.
    /// Security boundary: Admin sees all transactions; Employee/ShiftManager see only their warehouse's transactions.
    /// Transactions are filtered by their related Item's warehouse association.
    /// Parameters: role (from JWT ClaimTypes.Role), warehouseId (from JWT custom "WarehouseId" claim).
    /// </summary>
    Task<IEnumerable<TransactionDto>> GetAllAsync(string role, int? warehouseId);

    /// <summary>
    /// HIERARCHY: Retrieves all transactions visible to the user across their warehouse tree(s).
    /// Admin (owner): transactions for items in ALL owned main warehouses + sub-warehouses (DB-resolved).
    /// Employee/ShiftManager: transactions for items in their assigned main warehouse + sub-warehouses.
    /// </summary>
    Task<IEnumerable<TransactionDto>> GetAllForUserAsync(int userId, string role, int? userWarehouseId);

    Task<TransactionDto> CreateAsync(CreateTransactionDto dto);
}
