using AlonProject.Application.DTOs;

namespace AlonProject.Application.Interfaces;

public interface IProductCatalogService
{
    Task<ProductCatalogDto?> GetByIdAsync(int id);
    Task<ProductCatalogDto?> GetBySkuAsync(string sku);
    Task<IEnumerable<ProductCatalogDto>> GetAllAsync();
    Task<ProductCatalogDto> CreateAsync(CreateProductCatalogDto dto);
    Task<ProductCatalogDto> UpdateAsync(int id, CreateProductCatalogDto dto);
    Task<bool> DeleteAsync(int id);
}
