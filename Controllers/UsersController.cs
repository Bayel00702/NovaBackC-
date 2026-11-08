using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineStore.API.Data;
using OnlineStore.API.DTOs;
using OnlineStore.API.Models;

namespace OnlineStore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public UsersController(ApplicationDbContext db)
    {
        _db = db;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Список пользователей: /api/users?search=ivan
    [HttpGet]
    public async Task<ActionResult<List<UserResponseDto>>> GetAll([FromQuery] string? search)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u => u.Email.ToLower().Contains(s) || u.FullName.ToLower().Contains(s));
        }

        var users = await query
            .OrderBy(u => u.Id)
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                Role = u.Role.ToString(),
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    // Назначить или снять роль админа
    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> UpdateRole(int id, UserRoleUpdateDto dto)
    {
        if (!Enum.IsDefined(dto.Role))
            return BadRequest(new { message = "Неизвестная роль" });

        // Свою роль менять нельзя, так в системе всегда остаётся хотя бы один админ
        if (id == CurrentUserId)
            return BadRequest(new { message = "Нельзя изменить собственную роль" });

        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        user.Role = dto.Role;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}