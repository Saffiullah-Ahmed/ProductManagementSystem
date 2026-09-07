namespace ProductManagementApi.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        // Foreign Key
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
    }
}