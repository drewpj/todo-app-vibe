using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TodoApp.Api.Auth;
using TodoApp.Api.Contracts;
using TodoApp.Api.Domain;
using TodoApp.Api.Infrastructure;
using TodoApp.Api.Options;
using TodoApp.Api.Services;
using TodoApp.Tests.Support;

namespace TodoApp.Tests.Unit;

public sealed class AuthServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly CancellationToken _ct = CancellationToken.None;

    public void Dispose() => _db.Dispose();

    private AuthService CreateService()
    {
        var jwt = Microsoft.Extensions.Options.Options.Create(new JwtOptions { SigningKey = ApiFactory.SigningKey });
        return new AuthService(_db.CreateContext(), new PasswordHasher<User>(), new TokenService(jwt, _clock), _clock);
    }

    [Fact]
    public async Task Register_stores_normalised_email_and_a_hashed_password()
    {
        var response = await CreateService().RegisterAsync(new RegisterRequest("  Jane@Example.COM ", "s3cret-pass"), _ct);

        Assert.Equal("jane@example.com", response.User.Email);
        Assert.False(string.IsNullOrEmpty(response.AccessToken));

        await using var context = _db.CreateContext();
        var stored = await context.Users.SingleAsync();
        Assert.NotEqual("s3cret-pass", stored.PasswordHash);
        Assert.DoesNotContain("s3cret-pass", stored.PasswordHash);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email_ignoring_case()
    {
        await CreateService().RegisterAsync(new RegisterRequest("dup@example.com", "s3cret-pass"), _ct);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService().RegisterAsync(new RegisterRequest("DUP@example.com", "another-pass"), _ct));
    }

    [Fact]
    public async Task Login_succeeds_with_correct_password_regardless_of_email_case()
    {
        await CreateService().RegisterAsync(new RegisterRequest("login@example.com", "s3cret-pass"), _ct);

        var response = await CreateService().LoginAsync(new LoginRequest("LOGIN@example.com", "s3cret-pass"), _ct);

        Assert.Equal("login@example.com", response.User.Email);
    }

    [Fact]
    public async Task Login_fails_identically_for_wrong_password_and_unknown_user()
    {
        await CreateService().RegisterAsync(new RegisterRequest("real@example.com", "s3cret-pass"), _ct);

        var wrongPassword = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            CreateService().LoginAsync(new LoginRequest("real@example.com", "nope-nope"), _ct));
        var unknownUser = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            CreateService().LoginAsync(new LoginRequest("ghost@example.com", "s3cret-pass"), _ct));

        Assert.Equal(wrongPassword.Message, unknownUser.Message);
    }
}
