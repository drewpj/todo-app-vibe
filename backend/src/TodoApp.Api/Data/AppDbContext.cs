using Microsoft.EntityFrameworkCore;
using TodoApp.Api.Domain;

namespace TodoApp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<TodoItem> Todos => Set<TodoItem>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.HasKey(u => u.Id);
            user.Property(u => u.Email).HasMaxLength(254).IsRequired();
            user.Property(u => u.PasswordHash).IsRequired();
            user.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<TodoItem>(todo =>
        {
            todo.HasKey(t => t.Id);
            todo.Property(t => t.Title).HasMaxLength(TodoItem.TitleMaxLength).IsRequired();
            todo.Property(t => t.Description).HasMaxLength(TodoItem.DescriptionMaxLength);

            todo.HasOne(t => t.User)
                .WithMany(u => u.Todos)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Every read is "my todos, newest first", so lead with the owner.
            todo.HasIndex(t => new { t.UserId, t.CreatedAt });
        });
    }
}
