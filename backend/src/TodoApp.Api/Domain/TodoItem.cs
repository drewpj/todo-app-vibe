namespace TodoApp.Api.Domain;

public class TodoItem
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owner. Every query against todos is scoped by this value.</summary>
    public Guid UserId { get; set; }

    public User? User { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public bool IsCompleted { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public DateTime? DueDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Marks the item complete/incomplete and keeps <see cref="CompletedAt"/> consistent.</summary>
    public void SetCompletion(bool isCompleted, DateTime now)
    {
        if (IsCompleted == isCompleted)
        {
            return;
        }

        IsCompleted = isCompleted;
        CompletedAt = isCompleted ? now : null;
        UpdatedAt = now;
    }
}
