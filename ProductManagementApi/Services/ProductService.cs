using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ProductManagementApi.Data;
using ProductManagementApi.Models;
using ProductManagementApi.DTOs;

namespace ProductManagementApi.Services
{
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProductService(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<PagedResult<Product>> GetAllProductsAsync(string? search, int? categoryId, string? sortBy, string? sortOrder, int pageNumber, int pageSize)
        {
            var query = _context.Products.Include(p => p.Category).AsQueryable();

            // 1. Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.Name.Contains(search) || p.Description.Contains(search));
            }

            // 2. Apply category filter
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            // 3. Apply database-level sorting
            bool isDescending = !string.IsNullOrEmpty(sortOrder) && sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

            query = sortBy?.ToLowerInvariant() switch
            {
                "name" => isDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                "price" => isDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
                "stock" => isDescending ? query.OrderByDescending(p => p.Stock) : query.OrderBy(p => p.Stock),
                "createddate" => isDescending ? query.OrderByDescending(p => p.CreatedDate) : query.OrderBy(p => p.CreatedDate),
                _ => query.OrderBy(p => p.Id) // Default safe ordering
            };

            // 4. Get total count of matching records after filtering, before pagination
            var totalItems = await query.CountAsync();

            // 5. Apply database-level pagination using Skip and Take
            var items = await query
              .Skip((pageNumber - 1) * pageSize)
              .Take(pageSize)
              .ToListAsync();

            return new PagedResult<Product>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems
            };
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            return await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task CreateProductAsync(Product product, IFormFile? image)
        {
            if (image != null && image.Length > 0)
            {
                product.ImageUrl = await SaveImageAsync(image);
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateProductAsync(Product product, IFormFile? image)
        {
            var existingProduct = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            if (existingProduct == null) return;

            if (image != null && image.Length > 0)
            {
                if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
                {
                    DeletePhysicalImage(existingProduct.ImageUrl);
                }
                product.ImageUrl = await SaveImageAsync(image);
            }
            else
            {
                product.ImageUrl = existingProduct.ImageUrl;
            }

            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteProductAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    DeletePhysicalImage(product.ImageUrl);
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
        }

        private async Task<string> SaveImageAsync(IFormFile image)
        {
            var permittedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(extension) || !permittedExtensions.Contains(extension))
            {
                throw new BadHttpRequestException("Invalid file type. Only JPG, JPEG, PNG, and WEBP files are allowed.");
            }

            string uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "images", "products");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = $"{Guid.NewGuid()}{extension}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }

            return $"images/products/{uniqueFileName}";
        }

        private void DeletePhysicalImage(string imageUrl)
        {
            try
            {
                string webRootPath = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                string fullPath = Path.Combine(webRootPath, imageUrl.Replace("/", Path.DirectorySeparatorChar.ToString()));

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch
            {
                // Ignore file deletion errors
            }
        }
    }
}