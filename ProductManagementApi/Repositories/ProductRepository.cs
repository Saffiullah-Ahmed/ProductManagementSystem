using Microsoft.EntityFrameworkCore;
using ProductManagementApi.Data;
using ProductManagementApi.Models;

namespace ProductManagementApi.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly ApplicationDbContext _context;

        public ProductRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            return await _context.Products.Include(p => p.Category).ToListAsync();
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task AddAsync(Product product)
        {
            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Product product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.Stock > 0 && p.Stock <= threshold)
                .ToListAsync();
        }

        public async Task<int> GetLowStockCountAsync(int threshold)
        {
            return await _context.Products
                .Where(p => p.Stock > 0 && p.Stock <= threshold)
                .CountAsync();
        }

        public async Task<int> GetOutOfStockCountAsync()
        {
            return await _context.Products
                .Where(p => p.Stock == 0)
                .CountAsync();
        }

        public async Task DeleteAsync(Product product)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
    }
}