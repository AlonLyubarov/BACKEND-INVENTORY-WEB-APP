using AlonProject.Domain.Entities;

namespace AlonProject.Domain.Interfaces;

/// <summary>
/// Data access contract for Reminder entity persistence operations.
/// </summary>
public interface IReminderRepository
{
    /// <summary>Retrieves a single reminder by its unique ID.</summary>
    Task<Reminder?> GetByIdAsync(int id);

    /// <summary>Retrieves all reminders belonging to the given user, ordered by date.</summary>
    Task<IEnumerable<Reminder>> GetByUserAsync(int userId);

    /// <summary>Creates and persists a new reminder.</summary>
    Task<Reminder> CreateAsync(Reminder entity);

    /// <summary>Deletes a reminder by ID. Returns false when not found.</summary>
    Task<bool> DeleteAsync(int id);
}
