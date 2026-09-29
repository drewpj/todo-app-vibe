using Microsoft.EntityFrameworkCore;
using TodoApp.Api.Contracts;
using TodoApp.Api.Data;
using TodoApp.Api.Domain;
using TodoApp.Api.Infrastructure;

namespace TodoApp.Api.Services;

/// <summary>
/// All operations take the owner's id and filter by it in the query itself. A todo that belongs to someone else
/// is indistinguishable from one that does not exist (404), so ids cannot be probed across users.
/// </summary>
public interface ITodoService
{
    Task<PagedResult<TodoDto>> ListAsync(Guid userId, TodoListQuery query, CancellationToken ct);

    Task<TodoDto> GetAsync(Guid userId, Guid id, CancellationToken ct);

    Task<TodoDto> CreateAsync(Guid userId, SaveTodoRequest request, CancellationToken ct);

    Task<TodoDto> UpdateAsync(Guid userId, Guid id, SaveTodoRequest request, CancellationToken ct);

    Task<TodoDto> SetCompletionAsync(Guid userId, Guid id, bool isCompleted, CancellationToken ct);

    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct);
}

public sealed class TodoService(AppDbContext db, TimeProvider clock) : ITodoService
{
    private const string LikeEscape = "\\";

    public async Task<PagedResult<TodoDto>> ListAsync(Guid userId, TodoListQuery query, CancellationToken ct)
    {
        var todos = db.Todos.AsNoTracking().Where(t => t.UserId == userId);

        todos = query.Status switch
        {
            TodoStatusFilter.Active => todos.Where(t => !t.IsCompleted),
            TodoStatusFilter.Completed => todos.Where(t => t.IsCompleted),
            _ => todos,
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            todos = todos.Where(t =>
                EF.Functions.Like(t.Title, pattern, LikeEscape) ||
                (t.Description != null && EF.Functions.Like(t.Description, pattern, LikeEscape)));
        }

        var total = await todos.CountAsync(ct);

        // Open items first, then newest. Id is a tie-breaker so paging is stable.
        var items = await todos
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new TodoDto(
                t.Id, t.Title, t.Description, t.IsCompleted, t.CompletedAt, t.DueDate, t.CreatedAt, t.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<TodoDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<TodoDto> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var todo = await db.Todos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct)
                   ?? throw new NotFoundException("Todo not found.");
        return ToDto(todo);
    }

    public async Task<TodoDto> CreateAsync(Guid userId, SaveTodoRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var todo = new TodoItem
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Description = NormalizeOptional(request.Description),
            DueDate = request.DueDate,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Todos.Add(todo);
        await db.SaveChangesAsync(ct);
        return ToDto(todo);
    }

    public async Task<TodoDto> UpdateAsync(Guid userId, Guid id, SaveTodoRequest request, CancellationToken ct)
    {
        var todo = await FindOwnedAsync(userId, id, ct);

        todo.Title = request.Title.Trim();
        todo.Description = NormalizeOptional(request.Description);
        todo.DueDate = request.DueDate;
        todo.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);
        return ToDto(todo);
    }

    public async Task<TodoDto> SetCompletionAsync(Guid userId, Guid id, bool isCompleted, CancellationToken ct)
    {
        var todo = await FindOwnedAsync(userId, id, ct);

        todo.SetCompletion(isCompleted, clock.GetUtcNow().UtcDateTime);

        await db.SaveChangesAsync(ct);
        return ToDto(todo);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var deleted = await db.Todos.Where(t => t.Id == id && t.UserId == userId).ExecuteDeleteAsync(ct);
        if (deleted == 0)
        {
            throw new NotFoundException("Todo not found.");
        }
    }

    private async Task<TodoItem> FindOwnedAsync(Guid userId, Guid id, CancellationToken ct) =>
        await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct)
        ?? throw new NotFoundException("Todo not found.");

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape).Replace("%", LikeEscape + "%").Replace("_", LikeEscape + "_");

    private static TodoDto ToDto(TodoItem t) => new(
        t.Id, t.Title, t.Description, t.IsCompleted, t.CompletedAt, t.DueDate, t.CreatedAt, t.UpdatedAt);
}
