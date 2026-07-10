using AlonProject.Domain.Enums;

namespace AlonProject.Domain.Entities;

/// <summary>
/// Represents a user account in the inventory management system.
/// Each user has authentication credentials (hashed password, never stored in plain text),
/// a role for authorization, and belongs to exactly one warehouse.
/// </summary>
public class User
{
    /// <summary>
    /// Unique identifier for the user.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Username for login. Must be unique across the system.
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// User's email address for notifications and account recovery.
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// BCrypt-hashed password. NEVER store plain text passwords.
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// User's role determining what operations they can perform.
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Foreign key: The warehouse this user is assigned to.
    /// Initially null at registration; assigned later via invitation flow.
    /// </summary>
    public int? WarehouseId { get; set; }

    /// <summary>
    /// Navigation property: The warehouse this user belongs to (null until assigned).
    /// For Employees/ShiftManagers: the main warehouse they were invited to.
    /// For Admins: kept for compatibility; use OwnedWarehouses for actual ownership.
    /// </summary>
    public Warehouse? Warehouse { get; set; }

    /// <summary>
    /// Navigation property: Warehouses owned by this Admin user.
    /// Empty for non-Admin users.
    /// Only includes main warehouses (ParentWarehouseId = null).
    /// </summary>
    public ICollection<Warehouse> OwnedWarehouses { get; set; } = new List<Warehouse>();

    /// <summary>
    /// Whether the user's email address has been verified.
    /// Self-registered owners must verify before they can sign in;
    /// invited users are created verified (the owner supplied their email).
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// One-time token sent in the verification email link. Null once verified.
    /// </summary>
    public string? EmailVerificationToken { get; set; }

    /// <summary>
    /// Expiry of the verification token (UTC). Null once verified.
    /// </summary>
    public DateTime? EmailVerificationTokenExpiresAt { get; set; }

    /// <summary>
    /// When the last verification email was sent (UTC). Used to enforce a
    /// per-account resend cooldown on top of the per-IP rate limit.
    /// </summary>
    public DateTime? LastVerificationEmailSentAt { get; set; }

    /// <summary>
    /// When this user account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this user account was last modified (password change, role update, etc.).
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
