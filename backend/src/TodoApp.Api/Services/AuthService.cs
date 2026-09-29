using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TodoApp.Api.Auth;
using TodoApp.Api.Contracts;
using TodoApp.Api.Data;
using TodoApp.Api.Domain;
using TodoApp.Api.Infrastructure;

namespace TodoApp.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);

    Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct);
}

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    ITokenService tokens,
    TimeProvider clock) : IAuthService
{
    // Verified against when the e-mail is unknown, so "no such user" and "wrong password" cost the same time.
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<User>().HashPassword(new User { Email = "", PasswordHash = "" }, Guid.NewGuid().ToString()));

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = Normalize(request.Email);

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new User { Email = email, PasswordHash = string.Empty, CreatedAt = clock.GetUtcNow().UtcDateTime };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent registration; the unique index is the source of truth.
            throw new ConflictException("An account with this email already exists.");
        }

        return ToResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = Normalize(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            hasher.VerifyHashedPassword(new User { Email = "", PasswordHash = "" }, DummyHash.Value, request.Password);
            throw new AuthenticationFailedException();
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new AuthenticationFailedException();
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }

        return ToResponse(user);
    }

    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("User not found.");
        return new UserDto(user.Id, user.Email);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private AuthResponse ToResponse(User user)
    {
        var token = tokens.Issue(user);
        return new AuthResponse(token.Value, token.ExpiresAt, new UserDto(user.Id, user.Email));
    }
}
