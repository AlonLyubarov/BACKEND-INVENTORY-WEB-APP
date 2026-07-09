namespace AlonProject.Application.Interfaces;

/// <summary>
/// Centralized warehouse access-control rule.
/// Single source of truth used by ALL endpoints that touch a warehouse node:
/// - Admin: allowed if they OWN the root (main) warehouse of the target node.
/// - Employee/ShiftManager: allowed if the root of the target node equals their assigned main warehouse.
/// DB-based (ownership resolved against the database), not claim-only.
/// </summary>
public interface IWarehouseAccessService
{
    /// <summary>
    /// Checks whether the given user may access the specified warehouse node (main or sub).
    /// </summary>
    /// <param name="warehouseId">Target warehouse node ID (main or sub).</param>
    /// <param name="userId">Authenticated user ID (from NameIdentifier claim).</param>
    /// <param name="role">Authenticated user's role name (from Role claim).</param>
    /// <param name="userWarehouseId">Employee's main warehouse ID (from WarehouseId claim); null for admins/unassigned.</param>
    /// <returns>True if access is allowed per the single access rule.</returns>
    Task<bool> CanAccessWarehouseAsync(int warehouseId, int userId, string role, int? userWarehouseId);
}
