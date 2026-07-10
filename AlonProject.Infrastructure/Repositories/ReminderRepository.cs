using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using AlonProject.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlonProject.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IReminderRepository.
/// </summary>
public class ReminderRepository : IReminderRepository
{
    private readonly AppDbContext _context;

    public ReminderRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Reminder?> GetByIdAsync(int id)
    {
        return await _context.Reminders.FindAsync(id);
    }

    public async Task<IEnumerable<Reminder>> GetByUserAsync(int userId)
    {
        return await _context.Reminders
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Date)
            .ThenBy(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Reminder> CreateAsync(Reminder entity)
    {
        _context.Reminders.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var reminder = await _context.Reminders.FindAsync(id);
        if (reminder == null)
        {
            return false;
        }

        _context.Reminders.Remove(reminder);
        await _context.SaveChangesAsync();
        return true;
    }
}
