using JwtSecurityApi.Data;
using JwtSecurityApi.Dtos.Auth;
using Microsoft.AspNetCore.Mvc;

namespace JwtSecurityApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ApplicationDbContext dbContext) : ControllerBase
{
    /// <summary>Create a standard user account and return a JWT. NOT IMPLEMENTED YET.</summary>
    [HttpPost("register")]
    public Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        // TODO (README.md Step 4):
        //  1. normalize request.Email and reject duplicates with 409 Conflict
        //     (query dbContext.Users by NormalizedEmail, same pattern as AdminController).
        //  2. build an AppUser with Role = Roles.User — never trust a role from the client.
        //  3. hash request.Password with IPasswordHasher<AppUser> (inject it once Step 3 is done)
        //     and save the user with dbContext.SaveChangesAsync.
        //  4. issue a JWT with IJwtTokenService (inject it once Step 2 is done) and
        //     return StatusCode(201, new AuthResponse(...)).
        throw new NotImplementedException("Implement registration — see README.md Step 4.");
    }

    /// <summary>Authenticate a user and return a JWT. NOT IMPLEMENTED YET.</summary>
    [HttpPost("login")]
    public Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        // TODO (README.md Step 4):
        //  1. find the user by normalized email.
        //  2. verify the password with IPasswordHasher<AppUser>.VerifyHashedPassword.
        //  3. on success, issue a JWT and return an AuthResponse; on any failure, return a
        //     single generic 401 message — don't reveal whether the e-mail exists.
        throw new NotImplementedException("Implement login — see README.md Step 4.");
    }

    /// <summary>Return the current user's profile. NOT IMPLEMENTED YET.</summary>
    [HttpGet("me")]
    public Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        // TODO (README.md Step 5):
        //  1. add [Authorize] above this action once JWT authentication is wired up.
        //  2. read the "sub" claim from User (JwtRegisteredClaimNames.Sub).
        //  3. reload the user from dbContext.Users and return UserResponse.FromEntity(user).
        throw new NotImplementedException("Implement /me — see README.md Step 5.");
    }
}
