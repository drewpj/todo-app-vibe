using System.ComponentModel.DataAnnotations;

namespace TodoApp.Api.Contracts;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password);

public record LoginRequest(
    [Required, MaxLength(254)] string Email,
    [Required, MaxLength(128)] string Password);

public record UserDto(Guid Id, string Email);

public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserDto User);
