using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using JwtSecurityApi.Data;
using JwtSecurityApi.Dtos.Products;
using JwtSecurityApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JwtSecurityApi.Controllers;

// TODO (README.md Step 5): restore [Authorize] on this controller once JWT
// authentication is wired up, so every action below requires a valid token.
[ApiController]
[Route("api/products")]
public sealed class ProductsController(ApplicationDbContext dbContext)
    : ControllerBase
{
    /// <summary>List all products. Currently public — will require authentication after Step 5.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .OrderBy(product => product.Name)
            .Select(product => new ProductResponse(
                product.Id,
                product.Name,
                product.Price,
                product.Stock,
                product.CreatedAtUtc,
                product.CreatedByUserId))
            .ToListAsync(cancellationToken);

        return Ok(products);
    }

    /// <summary>Get a single product by id.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return product is null
            ? NotFound()
            : Ok(ProductResponse.FromEntity(product));
    }

    /// <summary>Create a product. Blocked (401) until JWT auth is wired — see Step 4/5.</summary>
    // TODO (README.md Step 5): restore [Authorize(Roles = Roles.Admin)] here.
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            // Expected for now: there is no authenticated user until JWT auth exists.
            return Unauthorized();
        }

        var product = new Product
        {
            Name = request.Name.Trim(),
            Price = request.Price,
            Stock = request.Stock,
            CreatedByUserId = userId
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            ProductResponse.FromEntity(product));
    }

    /// <summary>Update an existing product.</summary>
    // TODO (README.md Step 5): restore [Authorize(Roles = Roles.Admin)] here.
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductResponse>> Update(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        product.Name = request.Name.Trim();
        product.Price = request.Price;
        product.Stock = request.Stock;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ProductResponse.FromEntity(product));
    }

    /// <summary>Delete a product.</summary>
    // TODO (README.md Step 5): restore [Authorize(Roles = Roles.Admin)] here.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(subject, out userId);
    }
}
