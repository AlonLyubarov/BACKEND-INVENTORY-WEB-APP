using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlonProject.Api.Controllers;

/// <summary>
/// REST API controller for Product Catalog management.
/// TENANT SCOPING: catalogs belong to warehouse owners. Every request resolves
/// the caller's owner (Admin: self; Employee/SM: their tree's owner) and only
/// that owner's products are visible/manageable.
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
    /// Resolves the owner whose catalog the caller works with, from JWT claims.
    /// </summary>
    private async Task<int?> ResolveOwnerAsync()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
        {
            return null;
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        int? warehouseId = null;
        var warehouseClaim = User.FindFirst("WarehouseId");
        if (warehouseClaim != null && int.TryParse(warehouseClaim.Value, out var parsed))
        {
            warehouseId = parsed;
        }

        return await _service.ResolveOwnerIdAsync(userId, role, warehouseId);
    }

    /// <summary>
    /// GET api/productcatalog/{id}
    /// Retrieves a single product from the caller's catalog.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductCatalogDto>> GetById(int id)
    {
        var ownerId = await ResolveOwnerAsync();
        var product = await _service.GetByIdAsync(id, ownerId);
        if (product == null)
        {
            return NotFound();
        }
        return Ok(product);
    }

    /// <summary>
    /// GET api/productcatalog/sku/{sku}
    /// Retrieves a single product by SKU from the caller's catalog.
    /// </summary>
    [HttpGet("sku/{sku}")]
    public async Task<ActionResult<ProductCatalogDto>> GetBySku(string sku)
    {
        var ownerId = await ResolveOwnerAsync();
        var product = await _service.GetBySkuAsync(sku, ownerId);
        if (product == null)
        {
            return NotFound();
        }
        return Ok(product);
    }

    /// <summary>
    /// GET api/productcatalog
    /// Retrieves all products in the caller's catalog.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductCatalogDto>>> GetAll()
    {
        var ownerId = await ResolveOwnerAsync();
        var products = await _service.GetAllAsync(ownerId);
        _logger.LogInformation("API Response: Returned {ProductCount} products for owner {OwnerId}",
            products.Count(), ownerId);
        return Ok(products);
    }

    /// <summary>
    /// POST api/productcatalog
    /// Creates a new product in the caller's catalog.
    /// Requires: ShiftManager or Admin role
    /// </summary>
    [Authorize(Roles = "ShiftManager,Admin")]
    [HttpPost]
    public async Task<ActionResult<ProductCatalogDto>> Create(CreateProductCatalogDto dto)
    {
        var ownerId = await ResolveOwnerAsync();
        try
        {
            var product = await _service.CreateAsync(dto, ownerId);
            _logger.LogInformation("API Response: Product created. ID: {ProductId}, SKU: {Sku}, Owner: {OwnerId}",
                product.Id, dto.Sku, ownerId);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }
        catch (InvalidOperationException ex)
        {
            // Duplicate SKU within the caller's catalog
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// PUT api/productcatalog/{id}
    /// Updates a product in the caller's catalog.
    /// Requires: ShiftManager or Admin role
    /// </summary>
    [Authorize(Roles = "ShiftManager,Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<ProductCatalogDto>> Update(int id, CreateProductCatalogDto dto)
    {
        var ownerId = await ResolveOwnerAsync();
        try
        {
            var product = await _service.UpdateAsync(id, dto, ownerId);
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
    /// Deletes a product from the caller's catalog.
    /// Requires: ShiftManager or Admin role
    /// </summary>
    [Authorize(Roles = "ShiftManager,Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var ownerId = await ResolveOwnerAsync();
        var success = await _service.DeleteAsync(id, ownerId);
        if (!success)
        {
            return NotFound();
        }
        _logger.LogInformation("API Response: Product deleted. ID: {ProductId}", id);
        return NoContent();
    }
}
