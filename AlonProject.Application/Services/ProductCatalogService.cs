using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AlonProject.Application.Services;

/// <summary>
/// Service for managing product catalog operations.
/// TENANT SCOPING: every catalog belongs to one owner (Admin). Callers only
/// see and manage the catalog of their own warehouse tree's owner. Unowned
/// legacy rows (created before scoping) stay visible to everyone until edited,
/// at which point they are adopted by the editor's owner.
/// </summary>
public class ProductCatalogService : IProductCatalogService
{
    private readonly IProductCatalogRepository _repository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ILogger<ProductCatalogService> _logger;

    public ProductCatalogService(
        IProductCatalogRepository repository,
        IWarehouseRepository warehouseRepository,
        ILogger<ProductCatalogService> logger)
    {
        _repository = repository;
        _warehouseRepository = warehouseRepository;
        _logger = logger;
    }

    /// <summary>
    /// Resolves which owner's catalog the caller works with:
    /// Admin → themselves; Employee/ShiftManager → the owner of the root of
    /// their assigned warehouse tree.
    /// </summary>
    public async Task<int?> ResolveOwnerIdAsync(int userId, string role, int? warehouseId)
    {
        if (role == "Admin")
        {
            return userId;
        }

        if (warehouseId.HasValue)
        {
            var root = await _warehouseRepository.GetRootWarehouseAsync(warehouseId.Value);
            return root?.OwnerId;
        }

        return null;
    }

    /// <summary>True when the product is in the given owner's catalog (or legacy/unowned).</summary>
    private static bool IsVisible(ProductCatalog entity, int? ownerId)
    {
        return entity.OwnerId == null || entity.OwnerId == ownerId;
    }

    public async Task<ProductCatalogDto?> GetByIdAsync(int id, int? ownerId)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || !IsVisible(entity, ownerId))
        {
            _logger.LogWarning("Product not found or outside caller's catalog. ID: {ProductId}, Owner: {OwnerId}", id, ownerId);
            return null;
        }
        return MapToDto(entity);
    }

    public async Task<ProductCatalogDto?> GetBySkuAsync(string sku, int? ownerId)
    {
        // SKUs are unique per catalog, not globally — search within visibility
        var entities = await _repository.GetAllAsync();
        var entity = entities.FirstOrDefault(
            p => IsVisible(p, ownerId) && string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase));
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<IEnumerable<ProductCatalogDto>> GetAllAsync(int? ownerId)
    {
        var entities = await _repository.GetAllAsync();
        var visible = entities.Where(p => IsVisible(p, ownerId)).ToList();
        _logger.LogInformation("Retrieved {ProductCount} products for owner {OwnerId}", visible.Count, ownerId);
        return visible.Select(MapToDto);
    }

    public async Task<ProductCatalogDto> CreateAsync(CreateProductCatalogDto dto, int? ownerId)
    {
        _logger.LogInformation("Creating product. SKU: {Sku}, Owner: {OwnerId}", dto.Sku, ownerId);

        // SKU must be unique within this owner's catalog
        var duplicate = await GetBySkuAsync(dto.Sku, ownerId);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"SKU '{dto.Sku}' already exists in your catalog.");
        }

        var entity = new ProductCatalog
        {
            Sku = dto.Sku,
            Name = dto.Name,
            Price = dto.Price,
            Barcode = dto.Barcode,
            OwnerId = ownerId
        };

        var created = await _repository.CreateAsync(entity);
        _logger.LogInformation("Product created. ID: {ProductId}, SKU: {Sku}, Owner: {OwnerId}", created.Id, dto.Sku, ownerId);
        return MapToDto(created);
    }

    public async Task<ProductCatalogDto> UpdateAsync(int id, CreateProductCatalogDto dto, int? ownerId)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || !IsVisible(entity, ownerId))
        {
            throw new KeyNotFoundException($"Product with ID {id} not found");
        }

        entity.Sku = dto.Sku;
        entity.Name = dto.Name;
        entity.Price = dto.Price;
        entity.Barcode = dto.Barcode;
        // Editing a legacy (unowned) product adopts it into the editor's catalog
        entity.OwnerId ??= ownerId;
        entity.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(entity);
        _logger.LogInformation("Product updated. ID: {ProductId}, SKU: {Sku}", id, dto.Sku);
        return MapToDto(updated);
    }

    public async Task<bool> DeleteAsync(int id, int? ownerId)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || !IsVisible(entity, ownerId))
        {
            _logger.LogWarning("Product deletion refused — not found or outside caller's catalog. ID: {ProductId}", id);
            return false;
        }

        var result = await _repository.DeleteAsync(id);
        if (result)
        {
            _logger.LogInformation("Product deleted. ID: {ProductId}", id);
        }
        return result;
    }

    /// <summary>
    /// Maps ProductCatalog entity to ProductCatalogDto for API responses.
    /// </summary>
    private static ProductCatalogDto MapToDto(ProductCatalog entity)
    {
        return new ProductCatalogDto
        {
            Id = entity.Id,
            Sku = entity.Sku,
            Name = entity.Name,
            Price = entity.Price,
            Barcode = entity.Barcode,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
