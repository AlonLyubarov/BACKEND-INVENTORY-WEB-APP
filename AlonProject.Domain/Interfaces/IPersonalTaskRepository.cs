using AlonProject.Domain.Entities;

namespace AlonProject.Domain.Interfaces;

/// <summary>
/// Data access contract for PersonalTask persistence operations.
/// </summary>
public interface IPersonalTaskRepository
{
    /// <summary>Retrieves a single task by its unique ID.</summary>
    Task<PersonalTask?> GetByIdAsync(int id);

    /// <summary>
    /// Retrieves all tasks belonging to the given user, ordered so open tasks
    /// come first, then by due date (soonest first, undated last), then newest.
    /// </summary>
    Task<IEnumerable<PersonalTask>> GetByUserAsync(int userId);

    /// <summary>Creates and persists a new task.</summary>
    Task<PersonalTask> CreateAsync(PersonalTask entity);

    /// <summary>Persists changes to an existing task.</summary>
    Task<PersonalTask> UpdateAsync(PersonalTask entity);

    /// <summary>Deletes a task by ID. Returns false when not found.</summary>
    Task<bool> DeleteAsync(int id);
}
