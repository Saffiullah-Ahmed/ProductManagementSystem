namespace ProductManagementApi.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }

        // Add this property
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Foreign Key for Category
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
    }
}