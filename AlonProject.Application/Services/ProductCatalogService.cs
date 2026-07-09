using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using AlonProject.Domain.Entities;
using AlonProject.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AlonProject.Application.Services;

/// <summary>
/// Service for managing product catalog operations with comprehensive logging and business logic.
/// Handles CRUD operations on ProductCatalog entities with data mapping to DTOs.
/// </summary>
public class ProductCatalogService : IProductCatalogService
{
    private readonly IProductCatalogRepository _repository;
    private readonly ILogger<ProductCatalogService> _logger;

    public ProductCatalogService(IProductCatalogRepository repository, ILogger<ProductCatalogService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a product by its unique ID.
    /// </summary>
    public async Task<ProductCatalogDto?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Retrieving product with ID: {ProductId}", id);
        try
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
            {
                _logger.LogWarning("Product not found. ID: {ProductId}", id);
                return null;
            }
            _logger.LogDebug("Product retrieved successfully. ID: {ProductId}, SKU: {Sku}", id, entity.Sku);
            return MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product with ID: {ProductId}", id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a product by its SKU (Stock Keeping Unit).
    /// </summary>
    public async Task<ProductCatalogDto?> GetBySkuAsync(string sku)
    {
        _logger.LogInformation("Retrieving product by SKU: {Sku}", sku);
        try
        {
            var entity = await _repository.GetBySkuAsync(sku);
            if (entity == null)
            {
                _logger.LogWarning("Product not found by SKU: {Sku}", sku);
                return null;
            }
            _logger.LogDebug("Product retrieved by SKU successfully. ID: {ProductId}, SKU: {Sku}", entity.Id, sku);
            return MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product by SKU: {Sku}", sku);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all products from the catalog.
    /// </summary>
    public async Task<IEnumerable<ProductCatalogDto>> GetAllAsync()
    {
        _logger.LogInformation("Retrieving all products");
        try
        {
            var entities = await _repository.GetAllAsync();
            var count = entities.Count();
            _logger.LogInformation("Retrieved {ProductCount} products successfully", count);
            return entities.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all products");
            throw;
        }
    }

    /// <summary>
    /// Creates a new product in the catalog.
    /// </summary>
    public async Task<ProductCatalogDto> CreateAsync(CreateProductCatalogDto dto)
    {
        _logger.LogInformation("Creating new product. SKU: {Sku}, Name: {ProductName}", dto.Sku, dto.Name);
        try
        {
            var entity = new ProductCatalog
            {
                Sku = dto.Sku,
                Name = dto.Name,
                Price = dto.Price,
                Barcode = dto.Barcode
            };

            var created = await _repository.CreateAsync(entity);
            _logger.LogInformation("Product created successfully. ID: {ProductId}, SKU: {Sku}", created.Id, dto.Sku);
            return MapToDto(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product with SKU: {Sku}", dto.Sku);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing product in the catalog.
    /// </summary>
    public async Task<ProductCatalogDto> UpdateAsync(int id, CreateProductCatalogDto dto)
    {
        _logger.LogInformation("Updating product. ID: {ProductId}, SKU: {Sku}", id, dto.Sku);
        try
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
            {
                _logger.LogWarning("Product not found for update. ID: {ProductId}", id);
                throw new KeyNotFoundException($"Product with ID {id} not found");
            }

            entity.Sku = dto.Sku;
            entity.Name = dto.Name;
            entity.Price = dto.Price;
            entity.Barcode = dto.Barcode;
            entity.UpdatedAt = DateTime.UtcNow;

            var updated = await _repository.UpdateAsync(entity);
            _logger.LogInformation("Product updated successfully. ID: {ProductId}, SKU: {Sku}", id, dto.Sku);
            return MapToDto(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product with ID: {ProductId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a product from the catalog.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        _logger.LogInformation("Deleting product. ID: {ProductId}", id);
        try
        {
            var result = await _repository.DeleteAsync(id);
            if (result)
            {
                _logger.LogInformation("Product deleted successfully. ID: {ProductId}", id);
            }
            else
            {
                _logger.LogWarning("Product deletion failed - product not found. ID: {ProductId}", id);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product with ID: {ProductId}", id);
            throw;
        }
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
