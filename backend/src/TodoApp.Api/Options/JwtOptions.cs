using System.ComponentModel.DataAnnotations;

namespace TodoApp.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = "todo-app";

    [Required]
    public string Audience { get; init; } = "todo-app-clients";

    /// <summary>HMAC-SHA256 signing key. Must come from configuration/secrets - never committed.</summary>
    [Required, MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters.")]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 60 * 24 * 7)]
    public int ExpiryMinutes { get; init; } = 60;
}
