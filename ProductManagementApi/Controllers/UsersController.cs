using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductManagementApi.Data;
using ProductManagementApi.DTOs;
using System.Security.Claims;
using BCrypt.Net;

namespace ProductManagementApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Require authentication for all endpoints by default
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/users/me
        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetMyProfile()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            return Ok(new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role.Name,
                IsActive = user.IsActive
            });
        }

        // PUT: api/users/me
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            // Check if email is being changed and if it's already taken by another user
            if (!string.Equals(user.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
            {
                var emailExists = await _context.Users.AnyAsync(u => u.Email == dto.Email && u.Id != userId);
                if (emailExists)
                {
                    return BadRequest(new { message = "Email is already in use by another account." });
                }
                user.Email = dto.Email;
            }

            user.Name = dto.Name;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Profile updated successfully." });
        }

        // PUT: api/users/change-password
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            {
                return BadRequest(new { message = "Current password is incorrect." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password changed successfully." });
        }

        // GET: api/users
        [HttpGet]
        [Authorize(Roles = "Admin")] // Admin only
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers([FromQuery] string? search)
        {
            var query = _context.Users.Include(u => u.Role).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u => u.Name.Contains(search) || u.Email.Contains(search));
            }

            var users = await query
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role.Name,
                    IsActive = u.IsActive
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/users/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")] // Admin only
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            return Ok(new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role.Name,
                IsActive = user.IsActive
            });
        }

        // PUT: api/users/{id}/role
        [HttpPut("{id}/role")]
        [Authorize(Roles = "Admin")] // Admin only
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateUserRoleDto dto)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (currentUserId == id && dto.Role != "Admin")
            {
                return BadRequest(new { message = "You cannot remove your own Admin privileges." });
            }

            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == dto.Role);
            if (role == null)
            {
                return BadRequest(new { message = "Specified role does not exist." });
            }

            user.RoleId = role.Id;
            await _context.SaveChangesAsync();

            return Ok(new { message = "User role updated successfully." });
        }

        // PUT: api/users/{id}/status
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")] // Admin only
        public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusDto dtos)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (currentUserId == id && !dtos.IsActive)
            {
                return BadRequest(new { message = "You cannot deactivate your own account." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            user.IsActive = dtos.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"User status updated to {(user.IsActive ? "Active" : "Disabled")}." });
        }
    }

    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}