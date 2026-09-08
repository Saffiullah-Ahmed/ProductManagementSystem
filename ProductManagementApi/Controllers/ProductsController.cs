using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductManagementApi.DTOs;
using ProductManagementApi.Models;
using ProductManagementApi.Services;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? stockStatus = null)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 50) pageSize = 50;

        var pagedResult = await _productService.GetAllProductsAsync(search, categoryId, sortBy, sortOrder, pageNumber, pageSize, stockStatus);

        var productDtos = pagedResult.Items.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Stock = p.Stock,
            ImageUrl = p.ImageUrl,
            CreatedDate = p.CreatedDate,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? string.Empty,
            StockStatus = (_productService as ProductService)?.CalculateStockStatus(p.Stock) ?? "Available"
        });

        var response = new
        {
            items = productDtos,
            pageNumber = pagedResult.PageNumber,
            pageSize = pagedResult.PageSize,
            totalItems = pagedResult.TotalItems,
            totalPages = pagedResult.TotalPages
        };

        return Ok(response);
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStockProducts()
    {
        var products = await _productService.GetLowStockProductsAsync();
        var productDtos = products.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Stock = p.Stock,
            ImageUrl = p.ImageUrl,
            CreatedDate = p.CreatedDate,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? string.Empty,
            StockStatus = "Low Stock"
        });

        return Ok(productDtos);
    }

    [HttpGet("stock-stats")]
    public async Task<IActionResult> GetStockStats()
    {
        var lowStockCount = await _productService.GetLowStockCountAsync();
        var outOfStockCount = await _productService.GetOutOfStockCountAsync();

        return Ok(new
        {
            lowStockCount,
            outOfStockCount
        });
    }

    [HttpGet("deleted")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetDeletedProducts()
    {
        var products = await _productService.GetDeletedProductsAsync();
        var productDtos = products.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Stock = p.Stock,
            ImageUrl = p.ImageUrl,
            CreatedDate = p.CreatedDate,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? string.Empty,
            StockStatus = (_productService as ProductService)?.CalculateStockStatus(p.Stock) ?? "Available"
        });

        return Ok(productDtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var p = await _productService.GetProductByIdAsync(id);
        if (p == null)
            return NotFound();

        var productDto = new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Stock = p.Stock,
            ImageUrl = p.ImageUrl,
            CreatedDate = p.CreatedDate,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? string.Empty,
            StockStatus = (_productService as ProductService)?.CalculateStockStatus(p.Stock) ?? "Available"
        };

        return Ok(productDto);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromForm] CreateProductDto dto)
    {
        try
        {
            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                Price = dto.Price,
                Stock = dto.Stock,
                CategoryId = dto.CategoryId
            };

            await _productService.CreateProductAsync(product, dto.Image);

            var responseDto = new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                ImageUrl = product.ImageUrl,
                CreatedDate = product.CreatedDate,
                CategoryId = product.CategoryId,
                StockStatus = (_productService as ProductService)?.CalculateStockStatus(product.Stock) ?? "Available"
            };

            return CreatedAtAction(nameof(GetById), new { id = product.Id }, responseDto);
        }
        catch (BadHttpRequestException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Database save failed", detailed = innerMessage });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateProductDto dto)
    {
        try
        {
            var existing = await _productService.GetProductByIdAsync(id);
            if (existing == null)
                return NotFound();

            existing.Name = dto.Name;
            existing.Description = dto.Description;
            existing.Price = dto.Price;
            existing.Stock = dto.Stock;
            existing.CategoryId = dto.CategoryId;

            await _productService.UpdateProductAsync(existing, dto.Image);
            return NoContent();
        }
        catch (BadHttpRequestException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Update failed", detailed = innerMessage });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _productService.DeleteProductAsync(id);
        return NoContent();
    }

    [HttpPut("{id}/restore")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Restore(int id)
    {
        await _productService.RestoreProductAsync(id);
        return NoContent();
    }
}