# JwtSecurityApi — Practice Starter

This is a **stripped copy** of [`JwtSecurityApi-DotNet9`](../JwtSecurityApi-DotNet9): same models, same DTOs, same `DbContext`, same EF Core migration, same NuGet packages — but with **all the JWT / authentication / authorization code removed**. Your job is to add it back.

It builds and runs as-is (`dotnet build` succeeds with 0 errors). What doesn't work yet:

- `POST /api/auth/register` and `POST /api/auth/login` throw `NotImplementedException` (501).
- `GET /api/auth/me` throws `NotImplementedException` (501).
- `POST/PUT/DELETE /api/products` return `401 Unauthorized` unconditionally (there's no way to identify "the current user" without a JWT).
- Every endpoint is currently reachable without a token — nothing is protected.
- Swagger has no "Authorize" button.

Follow the steps below in order. Each one tells you exactly which file to touch and gives the code to add. When you're done, this project should behave identically to `JwtSecurityApi-DotNet9` (see its `README.md` "API reference" and "Testing the endpoints" sections for the exact expected status codes) — use that project as the answer key if you get stuck.

## What's already here

| Included | Not included (your job) |
|---|---|
| `Models/AppUser.cs`, `Models/Product.cs` | `Options/JwtOptions.cs` |
| `Data/ApplicationDbContext.cs` | `Services/IJwtTokenService.cs`, `Services/JwtTokenService.cs` |
| `Dtos/Auth/*`, `Dtos/Products/*` | `Services/DbSeeder.cs` |
| `Constants/Roles.cs`, `Constants/Policies.cs` | Password hashing in `AuthController` |
| `Controllers/*` (route skeletons, no auth) | JWT issuance/validation |
| `Migrations/` (schema for `Users` + `Products`) | `[Authorize]` attributes anywhere |
| NuGet packages: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`, `Swashbuckle.AspNetCore` | JWT Bearer authentication / `AdminOnly` policy wiring in `Program.cs` |
| `Program.cs` (controllers, EF Core, Swagger, migrations-at-startup) | Swagger Bearer security scheme |

The JWT Bearer package is already referenced in `JwtSecurityApi.csproj` — you don't need to `dotnet add package` anything to complete this exercise.

## Step 0 — Setup

```powershell
cd JwtSecurityApi-Practice
dotnet restore
dotnet build   # should succeed with 0 errors (1 harmless "unused parameter" warning in AuthController)

dotnet user-secrets init
# appsettings.Development.json already points at Server=MYPC\SQLEXPRESS / Database=JwtSecurityPracticeDb
# (a separate database from the main course project, so the two don't collide) — adjust the
# instance name in appsettings.Development.json if yours differs, or override it here instead:
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=JwtSecurityPracticeDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"

dotnet ef database update
```

At this point `dotnet run` already works and Swagger loads at `/swagger` — you just can't authenticate yet. Now build the missing pieces.

## Step 1 — `Options/JwtOptions.cs`

Create the folder `Options/` and the file:

```csharp
namespace JwtSecurityApi.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}
```

## Step 2 — `Services/IJwtTokenService.cs` and `JwtTokenService.cs`

Create the folder `Services/` and the two files:

```csharp
// Services/IJwtTokenService.cs
using JwtSecurityApi.Models;

namespace JwtSecurityApi.Services;

public interface IJwtTokenService
{
    JwtTokenResult CreateToken(AppUser user);
}

public sealed record JwtTokenResult(string AccessToken, DateTime ExpiresAtUtc);
```

```csharp
// Services/JwtTokenService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JwtSecurityApi.Models;
using JwtSecurityApi.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JwtSecurityApi.Services;

public sealed class JwtTokenService(IOptions<JwtOptions> jwtOptions)
    : IJwtTokenService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public JwtTokenResult CreateToken(AppUser user)
    {
        var issuedAtUtc = DateTime.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(_jwtOptions.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.UniqueName, user.FullName),
            new("role", user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: issuedAtUtc,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var serializedToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new JwtTokenResult(serializedToken, expiresAtUtc);
    }
}
```

## Step 3 — Register `IPasswordHasher<AppUser>` in `Program.cs`

Add this near the other `builder.Services.Add...` calls (replaces the `// TODO Step 3` comment):

```csharp
using JwtSecurityApi.Models;
using Microsoft.AspNetCore.Identity;

// ...

builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
```

`PasswordHasher<AppUser>` ships with ASP.NET Core Identity — no extra NuGet package needed.

## Step 4 — Implement `AuthController` Register and Login

Replace the whole file with:

```csharp
using JwtSecurityApi.Constants;
using JwtSecurityApi.Data;
using JwtSecurityApi.Dtos.Auth;
using JwtSecurityApi.Models;
using JwtSecurityApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JwtSecurityApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    ApplicationDbContext dbContext,
    IPasswordHasher<AppUser> passwordHasher,
    IJwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var normalizedEmail = email.ToUpperInvariant();

        var emailExists = await dbContext.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (emailExists)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Email already in use",
                Detail = "An account already exists with this email address.",
                Status = StatusCodes.Status409Conflict
            });
        }

        // The client never chooses its own role — that would allow privilege
        // escalation by simply sending role = Admin in the JSON body.
        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            Role = Roles.User
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, CreateAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Authentication failed",
                Detail = "Invalid email or password.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Authentication failed",
                Detail = "Invalid email or password.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(CreateAuthResponse(user));
    }

    // "Me" comes in Step 5, once [Authorize] and claim reading make sense to add.

    private AuthResponse CreateAuthResponse(AppUser user)
    {
        var token = jwtTokenService.CreateToken(user);
        return new AuthResponse(token.AccessToken, "Bearer", token.ExpiresAtUtc, UserResponse.FromEntity(user));
    }
}
```

Notice: the failure message is identical whether the email doesn't exist or the password is wrong — that avoids leaking which emails are registered. `SuccessRehashNeeded` opportunistically re-hashes with current parameters without forcing a password reset.

You'll add the `Me` action and `[Authorize]` back onto this controller in Step 5.

## Step 5 — Wire up JWT Bearer authentication, the `AdminOnly` policy, and Swagger

### 5.1 `Program.cs`

Add these `using`s at the top:

```csharp
using System.Text;
using JwtSecurityApi.Constants;
using JwtSecurityApi.Options;
using JwtSecurityApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; // already present
```

Replace the `// TODO Step 1` comment block with the validated options binding, placed **before** `builder.Services.AddSwaggerGen`:

```csharp
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
    .Validate(o => Encoding.UTF8.GetByteCount(o.Key) >= 32, "Jwt:Key must be at least 32 bytes for HMAC-SHA256.")
    .Validate(o => o.ExpirationMinutes is > 0 and <= 1440, "Jwt:ExpirationMinutes must be between 1 and 1440.")
    .ValidateOnStart();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("The Jwt configuration section is missing.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep claim names exactly as issued in the JWT

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "unique_name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireAuthenticatedUser().RequireRole(Roles.Admin));
});
```

Replace the `// TODO Step 2` and `// TODO Step 6` comments (token service + seeder registration):

```csharp
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<DbSeeder>(); // implemented in Step 6
```

Replace the `// TODO Step 5: add the Bearer AddSecurityDefinition...` comment inside `AddSwaggerGen`:

```csharp
options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
{
    Name = "Authorization",
    Description = "Paste only the JWT — Swagger prefixes it with 'Bearer' automatically.",
    In = ParameterLocation.Header,
    Type = SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT"
});

options.AddSecurityRequirement(new OpenApiSecurityRequirement
{
    {
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        },
        Array.Empty<string>()
    }
});
```

Finally, replace the `// TODO Step 5: add app.UseAuthentication...` comment:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

This must sit after `app.UseHttpsRedirection()` and before `app.MapControllers()` — authentication builds the identity, authorization checks it, so the order matters.

### 5.2 `AuthController.Me`

Add this action back to `AuthController` (needs `using System.IdentityModel.Tokens.Jwt;` and `using System.Security.Claims;` and `using Microsoft.AspNetCore.Authorization;`):

```csharp
[Authorize]
[HttpGet("me")]
public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
{
    var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    if (!Guid.TryParse(subject, out var userId))
    {
        return Unauthorized();
    }

    var user = await dbContext.Users
        .AsNoTracking()
        .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

    return user is null ? Unauthorized() : Ok(UserResponse.FromEntity(user));
}
```

### 5.3 Restore `[Authorize]` on `ProductsController` and `AdminController`

In `Controllers/ProductsController.cs`, add `using Microsoft.AspNetCore.Authorization;` and `using JwtSecurityApi.Constants;`, then:

- add `[Authorize]` above the class declaration;
- add `[Authorize(Roles = Roles.Admin)]` above `Create`, `Update`, and `Delete`.

In `Controllers/AdminController.cs`, add `using Microsoft.AspNetCore.Authorization;` and `using JwtSecurityApi.Constants;`, then add `[Authorize(Policy = Policies.AdminOnly)]` above the class declaration.

## Step 6 — `Services/DbSeeder.cs` (bootstrap the first admin)

```csharp
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
        var password = configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Admin not created: SeedAdmin:Email or SeedAdmin:Password missing.");
            return;
        }

        var normalizedEmail = email.ToUpperInvariant();
        if (await dbContext.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return;
        }

        var admin = new AppUser
        {
            FullName = "Administrator",
            Email = email,
            NormalizedEmail = normalizedEmail,
            Role = Roles.Admin
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);

        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Initial admin created for {Email}.", email);
    }
}
```

Replace the `// TODO Step 6: resolve DbSeeder...` comment in `Program.cs`'s `Development` block with:

```csharp
var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
await seeder.SeedAsync();
```

Then set the seed admin credentials and a real JWT key (≥ 32 bytes):

```powershell
$bytes = New-Object byte[] 64
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$key = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:Key" $key

dotnet user-secrets set "SeedAdmin:Email" "admin@example.com"
dotnet user-secrets set "SeedAdmin:Password" "Admin123!ChangeMe"
```

> If `RandomNumberGenerator]::Fill` errors in Windows PowerShell 5.1, use `[System.Security.Cryptography.RNGCryptoServiceProvider]::Create().GetBytes($bytes)` instead, or from Git Bash: `head -c 64 /dev/urandom | base64 -w 0`.

## Step 7 — Run and test

```powershell
dotnet run
```

Open `/swagger` — you should now see an **Authorize** button. Expected behavior, identical to the finished course project:

| Scenario | Status |
|---|---|
| Register / Login (valid credentials) | `201` / `200` with `accessToken` |
| `GET /api/products` with no token | `401` |
| `GET /api/products` with a `User` token | `200` |
| `POST /api/products` with a `User` token | `403` |
| `POST /api/products` with an `Admin` token | `201` |
| `GET /api/admin/users` with an `Admin` token | `200` |
| Tampered token (payload edited, signature no longer matches) | `401` |

If something doesn't match, compare your files against `../JwtSecurityApi-DotNet9` — every file you touched here has an equivalent finished version there, and its `README.md` walks through the same code with more explanation (see especially its "Tutorial: recreate this project from scratch" §7–§11).
