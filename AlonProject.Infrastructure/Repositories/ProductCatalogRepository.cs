using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using AlonProject.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlonProject.Infrastructure.Repositories;

public class ProductCatalogRepository : IProductCatalogRepository
{
    private readonly AppDbContext _context;

    public ProductCatalogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ProductCatalog?> GetByIdAsync(int id)
    {
        return await _context.ProductCatalogs.FindAsync(id);
    }

    public async Task<ProductCatalog?> GetBySkuAsync(string sku)
    {
        return await _context.ProductCatalogs.FirstOrDefaultAsync(p => p.Sku == sku);
    }

    public async Task<IEnumerable<ProductCatalog>> GetAllAsync()
    {
        return await _context.ProductCatalogs.ToListAsync();
    }

    public async Task<IEnumerable<ProductCatalog>> GetByOwnerAsync(int ownerId)
    {
        return await _context.ProductCatalogs
            .Where(p => p.OwnerId == ownerId)
            .ToListAsync();
    }

    public async Task<ProductCatalog> CreateAsync(ProductCatalog entity)
    {
        _context.ProductCatalogs.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<ProductCatalog> UpdateAsync(ProductCatalog entity)
    {
        _context.ProductCatalogs.Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity == null)
            return false;

        _context.ProductCatalogs.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}
