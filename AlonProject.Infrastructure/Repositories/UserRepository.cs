using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using AlonProject.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlonProject.Infrastructure.Repositories;

/// <summary>
/// User persistence repository implementing repository pattern for data access.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves a user by ID, including related warehouse data.
    /// </summary>
    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users
            .Include(u => u.Warehouse)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    /// Retrieves a user by username (case-sensitive), including related warehouse.
    /// Username is unique in the system.
    /// </summary>
    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .Include(u => u.Warehouse)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    /// <summary>
    /// Retrieves all users in the system, including related warehouses.
    /// </summary>
    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users
            .Include(u => u.Warehouse)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves all users assigned to the specified warehouse.
    /// </summary>
    public async Task<IEnumerable<User>> GetByWarehouseIdAsync(int warehouseId)
    {
        return await _context.Users
            .Include(u => u.Warehouse)
            .Where(u => u.WarehouseId == warehouseId)
            .ToListAsync();
    }

    /// <summary>
    /// Creates and persists a new user record.
    /// Password must already be hashed before calling this method.
    /// </summary>
    public async Task<User> CreateAsync(User entity)
    {
        _context.Users.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    /// <summary>
    /// Updates an existing user record.
    /// </summary>
    public async Task<User> UpdateAsync(User entity)
    {
        _context.Users.Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    /// <summary>
    /// Runs the given work inside a single database transaction.
    /// </summary>
    public async Task ExecuteInTransactionAsync(Func<Task> work)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await work();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Retrieves a user by their email-verification token (SHA-256 hash).
    /// </summary>
    public async Task<User?> GetByVerificationTokenAsync(string token)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
    }

    /// <summary>
    /// Retrieves a user by email address.
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    /// <summary>
    /// Permanently deletes a user record.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return false;
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Atomically creates a new owner (Admin) user together with their main warehouse.
    /// Uses an explicit database transaction so either both records persist or neither does.
    /// </summary>
    public async Task<(User User, Warehouse Warehouse)> CreateOwnerWithWarehouseAsync(User user, Warehouse warehouse)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            warehouse.OwnerId = user.Id;
            _context.Warehouses.Add(warehouse);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return (user, warehouse);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
