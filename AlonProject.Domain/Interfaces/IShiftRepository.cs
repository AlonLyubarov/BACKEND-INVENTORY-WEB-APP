using AlonProject.Domain.Entities;

namespace AlonProject.Domain.Interfaces;

/// <summary>Data access contract for work-schedule shifts.</summary>
public interface IShiftRepository
{
    Task<Shift?> GetByIdAsync(int id);

    /// <summary>All shifts of one user from the given date onward, ordered by date/time.</summary>
    Task<IEnumerable<Shift>> GetByUserAsync(int userId, DateTime fromDate);

    /// <summary>All shifts of one warehouse from the given date onward, ordered by date/time.</summary>
    Task<IEnumerable<Shift>> GetByWarehouseAsync(int warehouseId, DateTime fromDate);

    Task<Shift> CreateAsync(Shift entity);
    Task<bool> DeleteAsync(int id);
}
