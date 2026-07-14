using AlonProject.Domain.Entities;

namespace AlonProject.Domain.Interfaces;

/// <summary>
/// Data access contract for refresh token persistence.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>Persists a new refresh token.</summary>
    Task<RefreshToken> CreateAsync(RefreshToken entity);

    /// <summary>Looks up a token by its SHA-256 hash (includes revoked/expired ones).</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash);

    /// <summary>Persists changes to an existing token (e.g. marking it revoked).</summary>
    Task<RefreshToken> UpdateAsync(RefreshToken entity);

    /// <summary>Revokes every active token of a user (used on logout / password change).</summary>
    Task RevokeAllForUserAsync(int userId);
}
