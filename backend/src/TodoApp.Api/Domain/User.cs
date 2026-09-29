namespace TodoApp.Api.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Normalised (trimmed, lower-cased) e-mail address. Unique.</summary>
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<TodoItem> Todos { get; set; } = [];
}
