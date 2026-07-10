using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AlonProject.Application.Services;

/// <summary>
/// Personal calendar reminders, scoped to the owning user.
/// </summary>
public class ReminderService : IReminderService
{
    private readonly IReminderRepository _reminderRepository;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(IReminderRepository reminderRepository, ILogger<ReminderService> logger)
    {
        _reminderRepository = reminderRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<ReminderDto>> GetForUserAsync(int userId)
    {
        _logger.LogInformation("Retrieving reminders for user {UserId}", userId);
        var reminders = await _reminderRepository.GetByUserAsync(userId);
        return reminders.Select(MapToDto);
    }

    public async Task<ReminderDto> CreateAsync(int userId, CreateReminderDto dto)
    {
        _logger.LogInformation("Creating reminder for user {UserId} on {Date}: {Title}", userId, dto.Date, dto.Title);

        var entity = new Reminder
        {
            UserId = userId,
            Date = dto.Date.Date, // normalize: reminders are pinned to a day
            Title = dto.Title.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()
        };

        var created = await _reminderRepository.CreateAsync(entity);
        _logger.LogInformation("Reminder created. ID: {ReminderId}, User: {UserId}", created.Id, userId);
        return MapToDto(created);
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        var reminder = await _reminderRepository.GetByIdAsync(id);
        if (reminder == null || reminder.UserId != userId)
        {
            // SECURITY: not-owned reminders behave exactly like missing ones
            _logger.LogWarning("Reminder {ReminderId} not found (or not owned) for user {UserId}", id, userId);
            return false;
        }

        await _reminderRepository.DeleteAsync(id);
        _logger.LogInformation("Reminder {ReminderId} deleted by user {UserId}", id, userId);
        return true;
    }

    private static ReminderDto MapToDto(Reminder entity)
    {
        return new ReminderDto
        {
            Id = entity.Id,
            Date = entity.Date,
            Title = entity.Title,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt
        };
    }
}
