using System.Threading.Tasks;
using JwtSecurityApi.Constants;
using JwtSecurityApi.Data;
using JwtSecurityApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JwtSecurityApi.Services;

public sealed class DbSeeder(
    ApplicationDbContext dbContext,
    IPasswordHasher<AppUser> passwordHasher,
    IConfiguration configuration,
    ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var email = configuration["SeedAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["SeedAdmin:Password"]?.Trim();
        if(string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Admin non créé SeedAdmin Email ou password absent.");
            return;
        }
        var normalizedEmail = email.ToUpperInvariant();
        if(await dbContext.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            logger.LogInformation("Admin non créé, déjà existant : {Email}", email);
            return;
        }
        var admin = new AppUser
        {
            FullName = "Administrateur",
            Email = email,
            NormalizedEmail = normalizedEmail,
            Role = Roles.Admin
        };

        admin.PasswordHash=passwordHasher.HashPassword(admin,password);
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin initial créé pour : {Email}.", email);
    }
}