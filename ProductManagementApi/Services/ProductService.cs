using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ProductManagementApi.Data;
using ProductManagementApi.Models;

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

        public async Task<IEnumerable<Product>> GetAllProductsAsync(string? search, int? categoryId)
        {
            var query = _context.Products.Include(p => p.Category).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.Name.Contains(search) || p.Description.Contains(search));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            return await query.ToListAsync();
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
                // Delete old image if it exists
                if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
                {
                    DeletePhysicalImage(existingProduct.ImageUrl);
                }
                // Save new image
                product.ImageUrl = await SaveImageAsync(image);
            }
            else
            {
                // Retain existing image if no new file is uploaded
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
                // Delete physical image file from disk
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    DeletePhysicalImage(product.ImageUrl);
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
        }

        // Helper: Save Image to wwwroot/images/products
        private async Task<string> SaveImageAsync(IFormFile image)
        {
            // Validate allowed extensions
            var permittedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(extension) || !permittedExtensions.Contains(extension))
            {
                throw new BadHttpRequestException("Invalid file type. Only JPG, JPEG, PNG, and WEBP files are allowed.");
            }

            // Define target folder: wwwroot/images/products
            string uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "images", "products");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Generate safe, unique file name using a GUID
            string uniqueFileName = $"{Guid.NewGuid()}{extension}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }

            // Return relative URL path stored in DB
            return $"images/products/{uniqueFileName}";
        }

        // Helper: Remove physical file from disk
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
                // Ignore file deletion errors to prevent blocking main database transaction
            }
        }
    }
}