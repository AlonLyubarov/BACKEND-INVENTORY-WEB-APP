namespace AlonProject.Domain.Enums;

/// <summary>
/// User role designation for role-based access control (RBAC).
/// Determines what operations a user can perform in the system.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Basic warehouse staff member. Can view inventory and log stock movements.
    /// </summary>
    Employee = 0,

    /// <summary>
    /// Shift supervisor. Can create/update products and items, in addition to Employee permissions.
    /// </summary>
    ShiftManager = 1,

    /// <summary>
    /// System administrator. Full access to all operations including user and warehouse management.
    /// </summary>
    Admin = 2
}
