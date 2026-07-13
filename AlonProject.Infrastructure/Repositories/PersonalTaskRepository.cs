using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using AlonProject.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlonProject.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IPersonalTaskRepository.
/// </summary>
public class PersonalTaskRepository : IPersonalTaskRepository
{
    private readonly AppDbContext _context;

    public PersonalTaskRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PersonalTask?> GetByIdAsync(int id)
    {
        return await _context.PersonalTasks.FindAsync(id);
    }

    public async Task<IEnumerable<PersonalTask>> GetByUserAsync(int userId)
    {
        return await _context.PersonalTasks
            .Where(t => t.UserId == userId)
            // Open tasks first; then soonest due date (undated last); then newest.
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueDate == null)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<PersonalTask> CreateAsync(PersonalTask entity)
    {
        _context.PersonalTasks.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<PersonalTask> UpdateAsync(PersonalTask entity)
    {
        _context.PersonalTasks.Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var task = await _context.PersonalTasks.FindAsync(id);
        if (task == null)
        {
            return false;
        }

        _context.PersonalTasks.Remove(task);
        await _context.SaveChangesAsync();
        return true;
    }
}
