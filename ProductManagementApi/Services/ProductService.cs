using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProductManagementApi.Data;
using ProductManagementApi.Models;
using ProductManagementApi.DTOs;

namespace ProductManagementApi.Services
{
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public ProductService(ApplicationDbContext context, IWebHostEnvironment environment, IConfiguration configuration)
        {
            _context = context;
            _environment = environment;
            _configuration = configuration;
        }

        private int GetThreshold()
        {
            return _configuration.GetValue<int>("LowStockThreshold", 5);
        }

        public string CalculateStockStatus(int stock)
        {
            int threshold = GetThreshold();
            if (stock == 0) return "Out of Stock";
            if (stock <= threshold) return "Low Stock";
            return "Available";
        }

        public async Task<PagedResult<Product>> GetAllProductsAsync(string? search, int? categoryId, string? sortBy, string? sortOrder, int pageNumber, int pageSize, string? stockStatus = null)
        {
            var query = _context.Products.Include(p => p.Category).Where(p => !p.IsDeleted).AsQueryable();
            int threshold = GetThreshold();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.Name.Contains(search) || p.Description.Contains(search));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(stockStatus))
            {
                if (stockStatus.Equals("Low Stock", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(p => p.Stock > 0 && p.Stock <= threshold);
                }
                else if (stockStatus.Equals("Out of Stock", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(p => p.Stock == 0);
                }
                else if (stockStatus.Equals("Available", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(p => p.Stock > threshold);
                }
            }

            bool isDescending = !string.IsNullOrEmpty(sortOrder) && sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

            query = sortBy?.ToLowerInvariant() switch
            {
                "name" => isDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                "price" => isDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
                "stock" => isDescending ? query.OrderByDescending(p => p.Stock) : query.OrderBy(p => p.Stock),
                "createddate" => isDescending ? query.OrderByDescending(p => p.CreatedDate) : query.OrderBy(p => p.CreatedDate),
                _ => query.OrderBy(p => p.Id)
            };

            var totalItems = await query.CountAsync();

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
            return await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        }

        public async Task<IEnumerable<Product>> GetLowStockProductsAsync()
        {
            int threshold = GetThreshold();
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted && p.Stock > 0 && p.Stock <= threshold)
                .ToListAsync();
        }

        public async Task<int> GetLowStockCountAsync()
        {
            int threshold = GetThreshold();
            return await _context.Products
                .Where(p => !p.IsDeleted && p.Stock > 0 && p.Stock <= threshold)
                .CountAsync();
        }

        public async Task<int> GetOutOfStockCountAsync()
        {
            return await _context.Products
                .Where(p => !p.IsDeleted && p.Stock == 0)
                .CountAsync();
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
            var product = await _context.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product != null)
            {
                product.IsDeleted = true;
                _context.Entry(product).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Product>> GetDeletedProductsAsync()
        {
            return await _context.Products
                .IgnoreQueryFilters()
                .Include(p => p.Category)
                .Where(p => p.IsDeleted)
                .ToListAsync();
        }

        public async Task RestoreProductAsync(int id)
        {
            var product = await _context.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product != null && product.IsDeleted)
            {
                product.IsDeleted = false;
                _context.Entry(product).State = EntityState.Modified;
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