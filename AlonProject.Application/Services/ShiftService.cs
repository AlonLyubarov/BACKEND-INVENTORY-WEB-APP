using System.Globalization;
using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AlonProject.Application.Services;

/// <summary>
/// Work-schedule shifts: assignment validation and per-user/per-warehouse queries.
/// </summary>
public class ShiftService : IShiftService
{
    private readonly IShiftRepository _shiftRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ShiftService> _logger;

    public ShiftService(
        IShiftRepository shiftRepository,
        IUserRepository userRepository,
        ILogger<ShiftService> logger)
    {
        _shiftRepository = shiftRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<ShiftDto>> GetMineAsync(int userId)
    {
        var shifts = await _shiftRepository.GetByUserAsync(userId, DateTime.UtcNow.Date);
        return shifts.Select(MapToDto);
    }

    public async Task<IEnumerable<ShiftDto>> GetForWarehouseAsync(int warehouseId)
    {
        var shifts = await _shiftRepository.GetByWarehouseAsync(warehouseId, DateTime.UtcNow.Date);
        return shifts.Select(MapToDto);
    }

    public async Task<ShiftDto> CreateAsync(CreateShiftDto dto)
    {
        // The assigned user must be a team member of that warehouse
        var user = await _userRepository.GetByIdAsync(dto.UserId);
        if (user == null || user.WarehouseId != dto.WarehouseId)
        {
            throw new InvalidOperationException("The user is not a team member of this warehouse.");
        }

        var start = ParseTime(dto.StartTime);
        var end = ParseTime(dto.EndTime);
        if (end <= start)
        {
            throw new InvalidOperationException("End time must be after start time.");
        }

        var entity = new Shift
        {
            UserId = dto.UserId,
            WarehouseId = dto.WarehouseId,
            Date = dto.Date.Date,
            StartTime = start,
            EndTime = end,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()
        };

        var created = await _shiftRepository.CreateAsync(entity);
        _logger.LogInformation(
            "Shift created: user {UserId} at warehouse {WarehouseId} on {Date:yyyy-MM-dd} {Start}-{End}",
            dto.UserId, dto.WarehouseId, entity.Date, dto.StartTime, dto.EndTime);

        // Reload with navigation properties for the response
        var withNavigation = await _shiftRepository.GetByIdAsync(created.Id);
        return MapToDto(withNavigation!);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _shiftRepository.DeleteAsync(id);
    }

    public async Task<int?> GetShiftWarehouseIdAsync(int id)
    {
        var shift = await _shiftRepository.GetByIdAsync(id);
        return shift?.WarehouseId;
    }

    private static TimeSpan ParseTime(string value) =>
        TimeSpan.ParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture);

    private static ShiftDto MapToDto(Shift entity)
    {
        return new ShiftDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Username = entity.User?.Username ?? $"User #{entity.UserId}",
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.Name ?? $"Warehouse #{entity.WarehouseId}",
            Date = entity.Date,
            StartTime = entity.StartTime.ToString(@"hh\:mm"),
            EndTime = entity.EndTime.ToString(@"hh\:mm"),
            Notes = entity.Notes
        };
    }
}
