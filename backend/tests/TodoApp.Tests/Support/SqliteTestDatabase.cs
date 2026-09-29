using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TodoApp.Api.Data;
using TodoApp.Api.Domain;

namespace TodoApp.Tests.Support;

/// <summary>
/// A private in-memory SQLite database for service-level unit tests. Real SQLite (not the EF in-memory provider)
/// so constraints, LIKE semantics and ordering behave like production. Each <see cref="CreateContext"/> call
/// returns a fresh context so tests never rely on the change tracker's identity map.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;

    public SqliteTestDatabase()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var context = CreateContext();
        context.Database.Migrate();
    }

    public AppDbContext CreateContext() => new(_options);

    public async Task<User> AddUserAsync(string? email = null)
    {
        await using var context = CreateContext();
        var user = new User
        {
            Email = email ?? ApiTestHelpers.UniqueEmail(),
            PasswordHash = "not-a-real-hash",
            CreatedAt = DateTime.UtcNow,
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public void Dispose() => _connection.Dispose();
}
