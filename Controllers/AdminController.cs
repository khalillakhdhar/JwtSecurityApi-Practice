using JwtSecurityApi.Data;
using JwtSecurityApi.Dtos.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JwtSecurityApi.Controllers;

// TODO (README.md Step 5): restore [Authorize(Policy = Policies.AdminOnly)] here
// once the AdminOnly policy is registered in Program.cs.
[ApiController]
[Route("api/admin")]
public sealed class AdminController(ApplicationDbContext dbContext)
    : ControllerBase
{
    /// <summary>List all users. Currently public — will require the Admin policy after Step 5.</summary>
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetUsers(
        CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.FullName)
            .Select(user => new UserResponse(
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                user.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }
}
