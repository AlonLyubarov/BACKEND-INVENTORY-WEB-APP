using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using AlonProject.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlonProject.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IRefreshTokenRepository.
/// </summary>
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken> CreateAsync(RefreshToken entity)
    {
        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash)
    {
        return await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
    }

    public async Task<RefreshToken> UpdateAsync(RefreshToken entity)
    {
        _context.RefreshTokens.Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task RevokeAllForUserAsync(int userId)
    {
        var active = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();

        foreach (var token in active)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        if (active.Count > 0)
        {
            await _context.SaveChangesAsync();
        }
    }
}
