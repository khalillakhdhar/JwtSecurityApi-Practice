using System.Reflection;
using JwtSecurityApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using JwtSecurityApi.Models;
using Microsoft.AspNetCore.Identity;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The 'DefaultConnection' connection string is missing. Configure it with dotnet user-secrets.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ---------------------------------------------------------------------------
// Everything below this line is what you'll add back in — see README.md.
//
// TODO (README.md Step 1): bind & validate JwtOptions from the "Jwt" config
//      section (create Options/JwtOptions.cs first).
// TODO (README.md Step 3): builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
// TODO (README.md Step 2): builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
// TODO (README.md Step 6): builder.Services.AddScoped<DbSeeder>();
// TODO (README.md Step 5): builder.Services.AddAuthentication(...).AddJwtBearer(...);
//      and builder.Services.AddAuthorization(options => { ... AdminOnly policy ... });
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "JWT Security API (practice)",
        Version = "v1",
        Description = "Starter project — finish the JWT authentication described in README.md."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // TODO (README.md Step 5): add the Bearer AddSecurityDefinition/AddSecurityRequirement here.
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "JWT Security API v1");
        options.DisplayRequestDuration();
    });

    // Learning-project convenience only: apply pending migrations (or EnsureCreated as a
    // fallback if none exist yet) at Development startup.
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var migrations = dbContext.Database.GetMigrations();
    if (migrations.Any())
    {
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync();
    }

    // TODO (README.md Step 6): resolve DbSeeder from scope.ServiceProvider and call SeedAsync().
}

app.UseHttpsRedirection();

// TODO (README.md Step 5): add, in this order:
//   app.UseAuthentication();
//   app.UseAuthorization();
// (they must come after UseHttpsRedirection and before MapControllers)

app.MapControllers();

app.Run();
