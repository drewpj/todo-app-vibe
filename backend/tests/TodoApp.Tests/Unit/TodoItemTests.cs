using TodoApp.Api.Domain;

namespace TodoApp.Tests.Unit;

public class TodoItemTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SetCompletion_true_marks_complete_and_stamps_time()
    {
        var todo = new TodoItem { Title = "x", UpdatedAt = T0 };
        var now = T0.AddHours(1);

        todo.SetCompletion(true, now);

        Assert.True(todo.IsCompleted);
        Assert.Equal(now, todo.CompletedAt);
        Assert.Equal(now, todo.UpdatedAt);
    }

    [Fact]
    public void SetCompletion_false_clears_completion_time()
    {
        var todo = new TodoItem { Title = "x" };
        todo.SetCompletion(true, T0);

        todo.SetCompletion(false, T0.AddHours(1));

        Assert.False(todo.IsCompleted);
        Assert.Null(todo.CompletedAt);
    }

    [Fact]
    public void SetCompletion_is_idempotent_and_keeps_original_completion_time()
    {
        var todo = new TodoItem { Title = "x" };
        todo.SetCompletion(true, T0);

        todo.SetCompletion(true, T0.AddDays(1));

        Assert.Equal(T0, todo.CompletedAt);
        Assert.Equal(T0, todo.UpdatedAt);
    }
}
