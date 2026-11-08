using Microsoft.EntityFrameworkCore;
using OnlineStore.API.Models;

namespace OnlineStore.API.Data;

public static class DbSeeder
{
    // Создаёт первого админа, только если в базе нет ни одного
    public static async Task SeedAdminAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
            return;

        var email = config["Admin:Email"]?.Trim().ToLowerInvariant();
        var password = config["Admin:Password"];
        var fullName = config["Admin:FullName"] ?? "Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("В базе нет админа, а Admin:Email и Admin:Password не заданы");
            return;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            db.Users.Add(new User
            {
                Email = email,
                FullName = fullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Admin
            });
        }
        else
        {
            user.Role = UserRole.Admin;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Создан главный администратор {Email}", email);
    }
}