namespace AlonProject.Domain.Entities;

/// <summary>
/// A long-lived refresh token used to mint new short-lived access tokens without
/// forcing the user to log in again. Only the SHA-256 hash of the raw token is
/// stored, so a database leak cannot be replayed. Tokens are single-use: each
/// refresh revokes the old one and issues a new one (rotation).
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>SHA-256 hash of the raw token (the raw value is only ever sent to the client).</summary>
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the token is rotated out or the user logs out; null while active.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>True only while the token is neither expired nor revoked.</summary>
    public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;

    // Navigation property
    public User User { get; set; } = null!;
}
