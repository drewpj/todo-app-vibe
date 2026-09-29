using System.ComponentModel.DataAnnotations;
using TodoApp.Api.Domain;

namespace TodoApp.Api.Contracts;

/// <summary>Body for both create (POST) and full update (PUT).</summary>
public record SaveTodoRequest(
    [Required, MaxLength(TodoItem.TitleMaxLength)] string Title,
    [MaxLength(TodoItem.DescriptionMaxLength)] string? Description,
    DateTime? DueDate);

public record SetCompletionRequest(bool IsCompleted);

public record TodoDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsCompleted,
    DateTime? CompletedAt,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public enum TodoStatusFilter
{
    All,
    Active,
    Completed,
}

public class TodoListQuery
{
    public TodoStatusFilter Status { get; init; } = TodoStatusFilter.All;

    [MaxLength(100)]
    public string? Search { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
