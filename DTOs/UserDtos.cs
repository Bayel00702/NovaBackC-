using OnlineStore.API.Models;

namespace OnlineStore.API.DTOs;

public class UserResponseDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class UserRoleUpdateDto
{
    public UserRole Role { get; set; }
}