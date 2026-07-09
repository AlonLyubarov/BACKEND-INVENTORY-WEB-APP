using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using AlonProject.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlonProject.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IWarehouseRepository.
/// Provides database access for Warehouse entities.
/// </summary>
public class WarehouseRepository : IWarehouseRepository
{
    private readonly AppDbContext _context;

    public WarehouseRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves a warehouse by its ID.
    /// </summary>
    public async Task<Warehouse?> GetByIdAsync(int id)
    {
        return await _context.Warehouses.FindAsync(id);
    }

    /// <summary>
    /// Retrieves all warehouses.
    /// </summary>
    public async Task<IEnumerable<Warehouse>> GetAllAsync()
    {
        return await _context.Warehouses.ToListAsync();
    }

    /// <summary>
    /// Retrieves the first warehouse (useful for getting the default).
    /// </summary>
    public async Task<Warehouse?> GetFirstAsync()
    {
        return await _context.Warehouses.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Creates a new warehouse.
    /// </summary>
    public async Task<Warehouse> CreateAsync(Warehouse entity)
    {
        await _context.Warehouses.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    /// <summary>
    /// Updates an existing warehouse.
    /// </summary>
    public async Task<Warehouse> UpdateAsync(Warehouse entity)
    {
        _context.Warehouses.Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    /// <summary>
    /// Deletes a warehouse by ID.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var warehouse = await _context.Warehouses.FindAsync(id);
        if (warehouse == null)
        {
            return false;
        }

        _context.Warehouses.Remove(warehouse);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Checks whether the given user owns the ROOT (main) warehouse of the specified node.
    /// Resolves the parent chain first, then compares OwnerId.
    /// </summary>
    public async Task<bool> IsOwnerAsync(int warehouseId, int userId)
    {
        var root = await GetRootWarehouseAsync(warehouseId);
        return root != null && root.OwnerId == userId;
    }

    /// <summary>
    /// Retrieves all MAIN warehouses owned by the specified user, including their sub-warehouses.
    /// </summary>
    public async Task<IEnumerable<Warehouse>> GetByOwnerAsync(int ownerId)
    {
        return await _context.Warehouses
            .Where(w => w.OwnerId == ownerId && w.ParentWarehouseId == null)
            .Include(w => w.SubWarehouses)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves the direct sub-warehouses of the specified parent warehouse.
    /// </summary>
    public async Task<IEnumerable<Warehouse>> GetSubWarehousesAsync(int parentId)
    {
        return await _context.Warehouses
            .Where(w => w.ParentWarehouseId == parentId)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Resolves the ROOT (main) warehouse of any node by walking ParentWarehouseId until null.
    /// Guards against cyclic references with a bounded hop count.
    /// </summary>
    public async Task<Warehouse?> GetRootWarehouseAsync(int warehouseId)
    {
        var current = await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == warehouseId);

        // Bounded walk: one nesting level is expected, but guard against accidental deeper chains/cycles
        var hops = 0;
        while (current?.ParentWarehouseId != null && hops < 10)
        {
            current = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == current.ParentWarehouseId.Value);
            hops++;
        }

        return current;
    }
}
