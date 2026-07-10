using AlonProject.Application.DTOs;

namespace AlonProject.Application.Interfaces;

/// <summary>
/// Service contract for personal calendar reminders.
/// All operations are scoped to the calling user.
/// </summary>
public interface IReminderService
{
    /// <summary>All reminders of the given user, ordered by date.</summary>
    Task<IEnumerable<ReminderDto>> GetForUserAsync(int userId);

    /// <summary>Creates a reminder owned by the given user.</summary>
    Task<ReminderDto> CreateAsync(int userId, CreateReminderDto dto);

    /// <summary>
    /// Deletes the reminder if it exists AND belongs to the given user.
    /// Returns false otherwise (not found and not-owned are indistinguishable).
    /// </summary>
    Task<bool> DeleteAsync(int id, int userId);
}
