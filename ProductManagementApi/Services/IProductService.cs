using Microsoft.AspNetCore.Http;
using ProductManagementApi.Models;
using ProductManagementApi.DTOs; // Add reference

namespace ProductManagementApi.Services
{
    public interface IProductService
    {
        Task<PagedResult<Product>> GetAllProductsAsync(string? search, int? categoryId, int pageNumber, int pageSize); 
        Task<Product?> GetProductByIdAsync(int id);
        Task CreateProductAsync(Product product, IFormFile? image);
        Task UpdateProductAsync(Product product, IFormFile? image);
        Task DeleteProductAsync(int id);
    }
}