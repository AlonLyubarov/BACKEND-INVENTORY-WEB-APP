using AlonProject.Domain.Entities;
using AlonProject.Domain.Enums;
using AlonProject.Domain.Interfaces;
using AlonProject.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlonProject.Infrastructure.Repositories;

public class ItemRepository : IItemRepository
{
    private readonly AppDbContext _context;
    private readonly ITransactionRepository _transactionRepository;

    public ItemRepository(AppDbContext context, ITransactionRepository transactionRepository)
    {
        _context = context;
        _transactionRepository = transactionRepository;
    }

    // PRIVATE helper to load item with navigation, excluding soft-deleted items
    private IQueryable<Item> GetItemsWithNavigation() => 
        _context.Items
            .Where(i => !i.IsDeleted)  // SECURITY: Exclude soft-deleted items from all queries
            .Include(i => i.ProductCatalog);

    public async Task<Item?> GetByIdAsync(int id)
    {
        return await GetItemsWithNavigation().FirstOrDefaultAsync(i => i.Id == id);
    }

    // SECURITY: Get item by ID, verifying it belongs to the warehouse
    public async Task<Item?> GetByIdAsync(int id, int warehouseId)
    {
        return await GetItemsWithNavigation()
            .FirstOrDefaultAsync(i => i.Id == id && i.WarehouseId == warehouseId);
    }

    public async Task<IEnumerable<Item>> GetByProductCatalogIdAsync(int productCatalogId)
    {
        return await GetItemsWithNavigation()
            .Where(i => i.ProductCatalogId == productCatalogId)
            .ToListAsync();
    }

    // SECURITY: Get items by product catalog from specific warehouse
    public async Task<IEnumerable<Item>> GetByProductCatalogIdAsync(int productCatalogId, int warehouseId)
    {
        return await GetItemsWithNavigation()
            .Where(i => i.ProductCatalogId == productCatalogId && i.WarehouseId == warehouseId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Item>> GetByLocationAsync(string location)
    {
        return await GetItemsWithNavigation()
            .Where(i => i.Location == location)
            .ToListAsync();
    }

    // SECURITY: Get items by location from specific warehouse
    public async Task<IEnumerable<Item>> GetByLocationAsync(string location, int warehouseId)
    {
        return await GetItemsWithNavigation()
            .Where(i => i.Location == location && i.WarehouseId == warehouseId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Item>> GetAllAsync()
    {
        return await GetItemsWithNavigation().ToListAsync();
    }

    // SECURITY: Get all items from specific warehouse
    public async Task<IEnumerable<Item>> GetAllAsync(int warehouseId)
    {
        return await GetItemsWithNavigation()
            .Where(i => i.WarehouseId == warehouseId)
            .ToListAsync();
    }

    // HIERARCHY: Get items across a set of warehouse nodes (main + its sub-warehouses)
    public async Task<IEnumerable<Item>> GetByWarehouseIdsAsync(IEnumerable<int> warehouseIds)
    {
        var ids = warehouseIds.ToList();
        return await GetItemsWithNavigation()
            .Where(i => ids.Contains(i.WarehouseId))
            .ToListAsync();
    }

    public async Task<Item> CreateAsync(Item entity)
    {
        // SECURITY: Use database transaction to ensure atomicity between Item and Transaction audit records
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                _context.Items.Add(entity);
                await _context.SaveChangesAsync();

                // Log the item creation as a transaction using TransactionRepository
                var transactionRecord = new Transaction
                {
                    ItemId = entity.Id,
                    Type = TransactionType.StockIn,
                    Quantity = entity.Quantity,
                    Notes = "Item created"
                };
                await _transactionRepository.CreateAsync(transactionRecord);

                await transaction.CommitAsync();
                return entity;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }

    public async Task<Item> UpdateAsync(Item entity)
    {
        // SECURITY: Use database transaction to ensure atomicity between Item and Transaction audit records
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                // Get the original item to detect quantity changes
                var originalItem = await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == entity.Id);

                _context.Items.Update(entity);
                await _context.SaveChangesAsync();

                // If quantity changed, log it as a transaction
                if (originalItem != null && originalItem.Quantity != entity.Quantity)
                {
                    var quantityDifference = entity.Quantity - originalItem.Quantity;
                    var transactionType = quantityDifference > 0 ? TransactionType.StockIn : TransactionType.StockOut;
                    var transactionRecord = new Transaction
                    {
                        ItemId = entity.Id,
                        Type = transactionType,
                        Quantity = Math.Abs(quantityDifference),
                        Notes = $"Quantity updated from {originalItem.Quantity} to {entity.Quantity}"
                    };
                    await _transactionRepository.CreateAsync(transactionRecord);
                }

                await transaction.CommitAsync();
                return entity;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        // SECURITY: Use soft delete to preserve audit trail
        // Instead of physically deleting, mark as IsDeleted=true
        // This allows transaction history to remain queryable for audit purposes
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                var entity = await GetByIdAsync(id);
                if (entity == null)
                    return false;

                // If already soft-deleted, return success
                if (entity.IsDeleted)
                    return true;

                // Log the deletion as a transaction before marking as deleted
                var transactionRecord = new Transaction
                {
                    ItemId = entity.Id,
                    Type = TransactionType.StockOut,
                    Quantity = entity.Quantity,
                    Notes = "Item removed from inventory (soft deleted)"
                };
                await _transactionRepository.CreateAsync(transactionRecord);

                // Soft delete: mark as deleted but don't remove from database
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
