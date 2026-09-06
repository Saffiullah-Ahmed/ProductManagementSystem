using Microsoft.AspNetCore.Http;
using ProductManagementApi.Models;

namespace ProductManagementApi.Services
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllProductsAsync(string? search, int? categoryId);
        Task<Product?> GetProductByIdAsync(int id);
        Task CreateProductAsync(Product product, IFormFile? image);
        Task UpdateProductAsync(Product product, IFormFile? image);
        Task DeleteProductAsync(int id);
    }
}