namespace ProductManagementApi.Models
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Navigation property for relationship
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}