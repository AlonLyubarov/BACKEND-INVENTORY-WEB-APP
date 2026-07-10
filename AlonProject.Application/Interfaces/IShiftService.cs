using AlonProject.Application.DTOs;

namespace AlonProject.Application.Interfaces;

/// <summary>
/// Work-schedule service. Owners assign shifts to their warehouse's team;
/// every user sees their own schedule.
/// </summary>
public interface IShiftService
{
    /// <summary>The calling user's upcoming shifts (today onward).</summary>
    Task<IEnumerable<ShiftDto>> GetMineAsync(int userId);

    /// <summary>Upcoming schedule of a warehouse (today onward).</summary>
    Task<IEnumerable<ShiftDto>> GetForWarehouseAsync(int warehouseId);

    /// <summary>
    /// Assigns a shift. The target user must belong to the warehouse and the
    /// time range must be valid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Validation failure</exception>
    Task<ShiftDto> CreateAsync(CreateShiftDto dto);

    Task<bool> DeleteAsync(int id);

    /// <summary>The warehouse a shift belongs to (for authorization), or null.</summary>
    Task<int?> GetShiftWarehouseIdAsync(int id);
}
