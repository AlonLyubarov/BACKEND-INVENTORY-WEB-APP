using AlonProject.Application.Interfaces;
using AlonProject.Domain.Enums;
using AlonProject.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AlonProject.Application.Services;

/// <summary>
/// Implements the single warehouse access rule (see IWarehouseAccessService).
/// Admin -> must own the ROOT of the target node.
/// Employee/ShiftManager -> the ROOT of the target node must equal their main warehouse.
/// </summary>
public class WarehouseAccessService : IWarehouseAccessService
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ILogger<WarehouseAccessService> _logger;

    public WarehouseAccessService(IWarehouseRepository warehouseRepository, ILogger<WarehouseAccessService> logger)
    {
        _warehouseRepository = warehouseRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> CanAccessWarehouseAsync(int warehouseId, int userId, string role, int? userWarehouseId)
    {
        if (string.Equals(role, UserRole.Admin.ToString(), StringComparison.Ordinal))
        {
            // Admin: DB-based ownership check of the root warehouse
            var isOwner = await _warehouseRepository.IsOwnerAsync(warehouseId, userId);
            if (!isOwner)
            {
                _logger.LogWarning("Access denied: Admin {UserId} does not own root of warehouse {WarehouseId}", userId, warehouseId);
            }
            return isOwner;
        }

        // Employee/ShiftManager: root of target node must equal their main warehouse
        if (userWarehouseId == null)
        {
            _logger.LogWarning("Access denied: User {UserId} has no warehouse assignment", userId);
            return false;
        }

        var root = await _warehouseRepository.GetRootWarehouseAsync(warehouseId);
        if (root == null)
        {
            _logger.LogWarning("Access denied: Warehouse {WarehouseId} not found", warehouseId);
            return false;
        }

        var allowed = root.Id == userWarehouseId.Value;
        if (!allowed)
        {
            _logger.LogWarning("Access denied: User {UserId} (main warehouse {UserWarehouseId}) attempted access to warehouse {WarehouseId} (root {RootId})",
                userId, userWarehouseId, warehouseId, root.Id);
        }
        return allowed;
    }
}
