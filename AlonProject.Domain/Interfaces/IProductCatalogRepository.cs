using AlonProject.Domain.Entities;

namespace AlonProject.Domain.Interfaces;

public interface IProductCatalogRepository
{
    Task<ProductCatalog?> GetByIdAsync(int id);
    Task<ProductCatalog?> GetBySkuAsync(string sku);
    Task<IEnumerable<ProductCatalog>> GetAllAsync();
    Task<IEnumerable<ProductCatalog>> GetByOwnerAsync(int ownerId);
    Task<ProductCatalog> CreateAsync(ProductCatalog entity);
    Task<ProductCatalog> UpdateAsync(ProductCatalog entity);
    Task<bool> DeleteAsync(int id);
}
