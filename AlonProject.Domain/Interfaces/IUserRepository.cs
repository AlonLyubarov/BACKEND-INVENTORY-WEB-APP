using AlonProject.Domain.Entities;

namespace AlonProject.Domain.Interfaces;

/// <summary>
/// Data access contract for User entity persistence operations.
/// Implements repository pattern for database abstraction.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Retrieves a single user by their unique ID.
    /// </summary>
    /// <param name="id">User's unique identifier</param>
    /// <returns>User entity if found, null if not found</returns>
    Task<User?> GetByIdAsync(int id);

    /// <summary>
    /// Retrieves a user by username (case-sensitive).
    /// Username is unique, so exactly 0 or 1 match expected.
    /// </summary>
    /// <param name="username">Username to search for</param>
    /// <returns>User entity if found, null if not found</returns>
    Task<User?> GetByUsernameAsync(string username);

    /// <summary>
    /// Retrieves all users in the system with pagination support.
    /// </summary>
    /// <returns>Collection of all users</returns>
    Task<IEnumerable<User>> GetAllAsync();

    /// <summary>
    /// Creates and persists a new user record.
    /// Password should already be hashed before calling this method.
    /// </summary>
    /// <param name="entity">User entity to persist</param>
    /// <returns>Persisted User entity with generated ID</returns>
    Task<User> CreateAsync(User entity);

    /// <summary>
    /// Updates an existing user record.
    /// </summary>
    /// <param name="entity">User entity with updated values</param>
    /// <returns>Updated User entity</returns>
    Task<User> UpdateAsync(User entity);

    /// <summary>
    /// Retrieves all users assigned to the specified warehouse.
    /// </summary>
    /// <param name="warehouseId">Main warehouse ID</param>
    /// <returns>Users whose WarehouseId equals the given warehouse</returns>
    Task<IEnumerable<User>> GetByWarehouseIdAsync(int warehouseId);

    /// <summary>
    /// Permanently deletes a user record.
    /// </summary>
    /// <param name="id">User's unique identifier</param>
    /// <returns>True if the user existed and was deleted, false if not found</returns>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Retrieves a user by their email-verification token (SHA-256 hash).
    /// </summary>
    Task<User?> GetByVerificationTokenAsync(string token);

    /// <summary>
    /// Runs the given work inside a single database transaction.
    /// All repositories in the same request scope share the DbContext,
    /// so their writes commit or roll back together.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> work);

    /// <summary>
    /// Retrieves a user by email address.
    /// </summary>
    Task<User?> GetByEmailAsync(string email);

    /// <summary>
    /// Atomically creates a new owner (Admin) user together with their main warehouse.
    /// Both inserts run in one database transaction: either both succeed or neither is persisted.
    /// The warehouse's OwnerId is set to the newly created user's ID.
    /// </summary>
    /// <param name="user">User entity to persist (password already hashed, Role = Admin)</param>
    /// <param name="warehouse">Main warehouse entity to persist (ParentWarehouseId = null)</param>
    /// <returns>Tuple of persisted user and warehouse with generated IDs</returns>
    Task<(User User, Warehouse Warehouse)> CreateOwnerWithWarehouseAsync(User user, Warehouse warehouse);
}
