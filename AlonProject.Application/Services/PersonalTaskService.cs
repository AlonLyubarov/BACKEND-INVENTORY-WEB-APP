using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AlonProject.Application.Services;

/// <summary>
/// Personal to-do tasks, scoped to the owning user.
/// </summary>
public class PersonalTaskService : IPersonalTaskService
{
    private readonly IPersonalTaskRepository _repository;
    private readonly ILogger<PersonalTaskService> _logger;

    public PersonalTaskService(IPersonalTaskRepository repository, ILogger<PersonalTaskService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<PersonalTaskDto>> GetForUserAsync(int userId)
    {
        _logger.LogInformation("Retrieving personal tasks for user {UserId}", userId);
        var tasks = await _repository.GetByUserAsync(userId);
        return tasks.Select(MapToDto);
    }

    public async Task<PersonalTaskDto> CreateAsync(int userId, CreatePersonalTaskDto dto)
    {
        _logger.LogInformation("Creating personal task for user {UserId}: {Title}", userId, dto.Title);

        var entity = new PersonalTask
        {
            UserId = userId,
            Title = dto.Title.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            DueDate = dto.DueDate?.Date,
            IsCompleted = false
        };

        var created = await _repository.CreateAsync(entity);
        _logger.LogInformation("Personal task created. ID: {TaskId}, User: {UserId}", created.Id, userId);
        return MapToDto(created);
    }

    public async Task<PersonalTaskDto?> UpdateAsync(int id, int userId, UpdatePersonalTaskDto dto)
    {
        var task = await _repository.GetByIdAsync(id);
        if (task == null || task.UserId != userId)
        {
            // SECURITY: not-owned tasks behave exactly like missing ones
            _logger.LogWarning("Personal task {TaskId} not found (or not owned) for user {UserId}", id, userId);
            return null;
        }

        // Track the completion transition so CompletedAt reflects reality
        var wasCompleted = task.IsCompleted;

        task.Title = dto.Title.Trim();
        task.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        task.DueDate = dto.DueDate?.Date;
        task.IsCompleted = dto.IsCompleted;

        if (dto.IsCompleted && !wasCompleted)
        {
            task.CompletedAt = DateTime.UtcNow;
        }
        else if (!dto.IsCompleted)
        {
            task.CompletedAt = null;
        }

        var updated = await _repository.UpdateAsync(task);
        _logger.LogInformation("Personal task {TaskId} updated by user {UserId} (completed: {IsCompleted})",
            id, userId, updated.IsCompleted);
        return MapToDto(updated);
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        var task = await _repository.GetByIdAsync(id);
        if (task == null || task.UserId != userId)
        {
            _logger.LogWarning("Personal task {TaskId} not found (or not owned) for user {UserId}", id, userId);
            return false;
        }

        await _repository.DeleteAsync(id);
        _logger.LogInformation("Personal task {TaskId} deleted by user {UserId}", id, userId);
        return true;
    }

    private static PersonalTaskDto MapToDto(PersonalTask entity)
    {
        return new PersonalTaskDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Notes = entity.Notes,
            IsCompleted = entity.IsCompleted,
            DueDate = entity.DueDate,
            CreatedAt = entity.CreatedAt,
            CompletedAt = entity.CompletedAt
        };
    }
}
