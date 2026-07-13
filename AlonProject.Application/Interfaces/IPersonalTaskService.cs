using AlonProject.Application.DTOs;

namespace AlonProject.Application.Interfaces;

/// <summary>
/// Service contract for personal to-do tasks.
/// All operations are scoped to the calling user.
/// </summary>
public interface IPersonalTaskService
{
    /// <summary>All tasks of the given user (open first, then by due date).</summary>
    Task<IEnumerable<PersonalTaskDto>> GetForUserAsync(int userId);

    /// <summary>Creates a task owned by the given user.</summary>
    Task<PersonalTaskDto> CreateAsync(int userId, CreatePersonalTaskDto dto);

    /// <summary>
    /// Updates the task if it exists AND belongs to the given user.
    /// Returns null otherwise (not found and not-owned are indistinguishable).
    /// </summary>
    Task<PersonalTaskDto?> UpdateAsync(int id, int userId, UpdatePersonalTaskDto dto);

    /// <summary>
    /// Deletes the task if it exists AND belongs to the given user.
    /// Returns false otherwise.
    /// </summary>
    Task<bool> DeleteAsync(int id, int userId);
}
