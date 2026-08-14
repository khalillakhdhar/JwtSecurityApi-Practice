using System.ComponentModel.DataAnnotations;

namespace JwtSecurityApi.Dtos.Auth;

public sealed class RegisterRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,100}$",
        ErrorMessage = "Password must contain a lowercase letter, an uppercase letter and a digit.")]
    public string Password { get; set; } = string.Empty;
}
