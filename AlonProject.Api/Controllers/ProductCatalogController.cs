using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlonProject.Api.Controllers;

/// <summary>
/// REST API controller for Product Catalog management.
/// Provides endpoints for CRUD operations on product master data including SKU, pricing, and barcodes.
/// Route: /api/productcatalog
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProductCatalogController : ControllerBase
{
    private readonly IProductCatalogService _service;
    private readonly ILogger<ProductCatalogController> _logger;

    public ProductCatalogController(IProductCatalogService service, ILogger<ProductCatalogController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// GET api/productcatalog/{id}
    /// Retrieves a single product by its unique ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductCatalogDto>> GetById(int id)
    {
        _logger.LogInformation("API Request: GET product by ID: {ProductId}", id);
        var product = await _service.GetByIdAsync(id);
        if (product == null)
        {
            _logger.LogWarning("API Response: Product not found. ID: {ProductId}", id);
            return NotFound();
        }
        _logger.LogDebug("API Response: Product returned. ID: {ProductId}", id);
        return Ok(product);
    }

    /// <summary>
    /// GET api/productcatalog/sku/{sku}
    /// Retrieves a single product by its SKU code.
    /// </summary>
    [HttpGet("sku/{sku}")]
    public async Task<ActionResult<ProductCatalogDto>> GetBySku(string sku)
    {
        _logger.LogInformation("API Request: GET product by SKU: {Sku}", sku);
        var product = await _service.GetBySkuAsync(sku);
        if (product == null)
        {
            _logger.LogWarning("API Response: Product not found by SKU: {Sku}", sku);
            return NotFound();
        }
        _logger.LogDebug("API Response: Product returned by SKU. SKU: {Sku}", sku);
        return Ok(product);
    }

    /// <summary>
    /// GET api/productcatalog
    /// Retrieves all products in the catalog.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductCatalogDto>>> GetAll()
    {
        _logger.LogInformation("API Request: GET all products");
        var products = await _service.GetAllAsync();
        _logger.LogInformation("API Response: Returned {ProductCount} products", products.Count());
        return Ok(products);
    }

    /// <summary>
    /// POST api/productcatalog
    /// Creates a new product in the catalog.
    /// Requires: ShiftManager or Admin role
    /// </summary>
    [Authorize(Roles = "ShiftManager,Admin")]
    [HttpPost]
    public async Task<ActionResult<ProductCatalogDto>> Create(CreateProductCatalogDto dto)
    {
        _logger.LogInformation("API Request: POST create product. SKU: {Sku}, Name: {ProductName}", dto.Sku, dto.Name);
        var product = await _service.CreateAsync(dto);
        _logger.LogInformation("API Response: Product created. ID: {ProductId}, SKU: {Sku}", product.Id, dto.Sku);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    /// <summary>
    /// PUT api/productcatalog/{id}
    /// Updates an existing product in the catalog.
    /// Requires: ShiftManager or Admin role
    /// </summary>
    [Authorize(Roles = "ShiftManager,Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<ProductCatalogDto>> Update(int id, CreateProductCatalogDto dto)
    {
        _logger.LogInformation("API Request: PUT update product. ID: {ProductId}, SKU: {Sku}", id, dto.Sku);
        try
        {
            var product = await _service.UpdateAsync(id, dto);
            _logger.LogInformation("API Response: Product updated successfully. ID: {ProductId}", id);
            return Ok(product);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("API Response: Product not found for update. ID: {ProductId}", id);
            return NotFound();
        }
    }

    /// <summary>
    /// DELETE api/productcatalog/{id}
    /// Deletes a product from the catalog.
    /// Requires: ShiftManager or Admin role
    /// </summary>
    [Authorize(Roles = "ShiftManager,Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        _logger.LogInformation("API Request: DELETE product. ID: {ProductId}", id);
        var success = await _service.DeleteAsync(id);
        if (!success)
        {
            _logger.LogWarning("API Response: Product not found for deletion. ID: {ProductId}", id);
            return NotFound();
        }
        _logger.LogInformation("API Response: Product deleted successfully. ID: {ProductId}", id);
        return NoContent();
    }
}
