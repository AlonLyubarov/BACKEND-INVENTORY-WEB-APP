using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using AlonProject.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlonProject.Infrastructure.Repositories;

/// <summary>EF Core implementation of IShiftRepository.</summary>
public class ShiftRepository : IShiftRepository
{
    private readonly AppDbContext _context;

    public ShiftRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Shift?> GetByIdAsync(int id)
    {
        return await _context.Shifts
            .Include(s => s.User)
            .Include(s => s.Warehouse)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<Shift>> GetByUserAsync(int userId, DateTime fromDate)
    {
        return await _context.Shifts
            .Include(s => s.Warehouse)
            .Where(s => s.UserId == userId && s.Date >= fromDate)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Shift>> GetByWarehouseAsync(int warehouseId, DateTime fromDate)
    {
        return await _context.Shifts
            .Include(s => s.User)
            .Where(s => s.WarehouseId == warehouseId && s.Date >= fromDate)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<Shift> CreateAsync(Shift entity)
    {
        _context.Shifts.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var shift = await _context.Shifts.FindAsync(id);
        if (shift == null)
        {
            return false;
        }
        _context.Shifts.Remove(shift);
        await _context.SaveChangesAsync();
        return true;
    }
}
