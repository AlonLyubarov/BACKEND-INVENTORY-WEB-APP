using AlonProject.Application.DTOs;

namespace AlonProject.Application.Interfaces;

/// <summary>
/// Product catalog service contract. Catalogs are TENANT-SCOPED: every
/// operation receives the resolved owner (Admin) id and only sees that
/// owner's products (plus unowned legacy rows).
/// </summary>
public interface IProductCatalogService
{
    /// <summary>
    /// Resolves which owner's catalog the caller works with:
    /// Admins own their catalog; Employees/ShiftManagers use the catalog of
    /// the owner of their assigned warehouse tree.
    /// </summary>
    Task<int?> ResolveOwnerIdAsync(int userId, string role, int? warehouseId);

    Task<ProductCatalogDto?> GetByIdAsync(int id, int? ownerId);
    Task<ProductCatalogDto?> GetBySkuAsync(string sku, int? ownerId);
    Task<IEnumerable<ProductCatalogDto>> GetAllAsync(int? ownerId);
    Task<ProductCatalogDto> CreateAsync(CreateProductCatalogDto dto, int? ownerId);
    Task<ProductCatalogDto> UpdateAsync(int id, CreateProductCatalogDto dto, int? ownerId);
    Task<bool> DeleteAsync(int id, int? ownerId);
}
