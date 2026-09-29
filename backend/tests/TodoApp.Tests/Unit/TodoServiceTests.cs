using Microsoft.Extensions.Time.Testing;
using TodoApp.Api.Contracts;
using TodoApp.Api.Infrastructure;
using TodoApp.Api.Services;
using TodoApp.Tests.Support;

namespace TodoApp.Tests.Unit;

public sealed class TodoServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly CancellationToken _ct = CancellationToken.None;

    public void Dispose() => _db.Dispose();

    private TodoService CreateService() => new(_db.CreateContext(), _clock);

    private static SaveTodoRequest Req(string title, string? description = null) => new(title, description, null);

    [Fact]
    public async Task Create_trims_title_and_normalises_blank_description()
    {
        var user = await _db.AddUserAsync();

        var created = await CreateService().CreateAsync(user.Id, Req("  Write tests  ", "   "), _ct);

        Assert.Equal("Write tests", created.Title);
        Assert.Null(created.Description);
        Assert.False(created.IsCompleted);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, created.CreatedAt);
    }

    [Fact]
    public async Task Create_persists_todo_for_owner_only()
    {
        var owner = await _db.AddUserAsync();
        var other = await _db.AddUserAsync();

        var created = await CreateService().CreateAsync(owner.Id, Req("Mine"), _ct);

        Assert.Equal("Mine", (await CreateService().GetAsync(owner.Id, created.Id, _ct)).Title);
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetAsync(other.Id, created.Id, _ct));
    }

    [Fact]
    public async Task Update_changes_fields_and_bumps_updated_at()
    {
        var user = await _db.AddUserAsync();
        var created = await CreateService().CreateAsync(user.Id, Req("Old"), _ct);
        _clock.Advance(TimeSpan.FromMinutes(5));
        var due = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

        var updated = await CreateService().UpdateAsync(user.Id, created.Id, new SaveTodoRequest("New", "Details", due), _ct);

        Assert.Equal("New", updated.Title);
        Assert.Equal("Details", updated.Description);
        Assert.Equal(due, updated.DueDate);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_of_another_users_todo_is_not_found_and_leaves_it_unchanged()
    {
        var owner = await _db.AddUserAsync();
        var intruder = await _db.AddUserAsync();
        var created = await CreateService().CreateAsync(owner.Id, Req("Original"), _ct);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService().UpdateAsync(intruder.Id, created.Id, Req("Hijacked"), _ct));

        Assert.Equal("Original", (await CreateService().GetAsync(owner.Id, created.Id, _ct)).Title);
    }

    [Fact]
    public async Task SetCompletion_toggles_and_records_completion_time()
    {
        var user = await _db.AddUserAsync();
        var created = await CreateService().CreateAsync(user.Id, Req("Task"), _ct);
        _clock.Advance(TimeSpan.FromHours(1));

        var done = await CreateService().SetCompletionAsync(user.Id, created.Id, true, _ct);
        var undone = await CreateService().SetCompletionAsync(user.Id, created.Id, false, _ct);

        Assert.True(done.IsCompleted);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, done.CompletedAt);
        Assert.False(undone.IsCompleted);
        Assert.Null(undone.CompletedAt);
    }

    [Fact]
    public async Task Delete_removes_owned_todo_and_rejects_unknown_or_foreign_ids()
    {
        var owner = await _db.AddUserAsync();
        var other = await _db.AddUserAsync();
        var created = await CreateService().CreateAsync(owner.Id, Req("Temp"), _ct);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().DeleteAsync(other.Id, created.Id, _ct));
        await CreateService().DeleteAsync(owner.Id, created.Id, _ct);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetAsync(owner.Id, created.Id, _ct));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().DeleteAsync(owner.Id, Guid.NewGuid(), _ct));
    }

    [Fact]
    public async Task List_returns_only_the_callers_todos()
    {
        var alice = await _db.AddUserAsync();
        var bob = await _db.AddUserAsync();
        await CreateService().CreateAsync(alice.Id, Req("Alice 1"), _ct);
        await CreateService().CreateAsync(bob.Id, Req("Bob 1"), _ct);

        var result = await CreateService().ListAsync(alice.Id, new TodoListQuery(), _ct);

        var only = Assert.Single(result.Items);
        Assert.Equal("Alice 1", only.Title);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var user = await _db.AddUserAsync();
        var open = await CreateService().CreateAsync(user.Id, Req("Open"), _ct);
        var done = await CreateService().CreateAsync(user.Id, Req("Done"), _ct);
        await CreateService().SetCompletionAsync(user.Id, done.Id, true, _ct);

        var active = await CreateService().ListAsync(user.Id, new TodoListQuery { Status = TodoStatusFilter.Active }, _ct);
        var completed = await CreateService().ListAsync(user.Id, new TodoListQuery { Status = TodoStatusFilter.Completed }, _ct);
        var all = await CreateService().ListAsync(user.Id, new TodoListQuery(), _ct);

        Assert.Equal([open.Id], active.Items.Select(t => t.Id));
        Assert.Equal([done.Id], completed.Items.Select(t => t.Id));
        Assert.Equal(2, all.TotalCount);
    }

    [Fact]
    public async Task List_orders_open_items_first_then_newest_first()
    {
        var user = await _db.AddUserAsync();
        var first = await CreateService().CreateAsync(user.Id, Req("first"), _ct);
        _clock.Advance(TimeSpan.FromMinutes(1));
        var second = await CreateService().CreateAsync(user.Id, Req("second"), _ct);
        _clock.Advance(TimeSpan.FromMinutes(1));
        var third = await CreateService().CreateAsync(user.Id, Req("third"), _ct);
        await CreateService().SetCompletionAsync(user.Id, third.Id, true, _ct);

        var result = await CreateService().ListAsync(user.Id, new TodoListQuery(), _ct);

        Assert.Equal([second.Id, first.Id, third.Id], result.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task List_paginates()
    {
        var user = await _db.AddUserAsync();
        for (var i = 0; i < 5; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            await CreateService().CreateAsync(user.Id, Req($"todo {i}"), _ct);
        }

        var page2 = await CreateService().ListAsync(user.Id, new TodoListQuery { Page = 2, PageSize = 2 }, _ct);

        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(["todo 2", "todo 1"], page2.Items.Select(t => t.Title));
    }

    [Fact]
    public async Task Search_matches_title_or_description_case_insensitively()
    {
        var user = await _db.AddUserAsync();
        await CreateService().CreateAsync(user.Id, Req("Buy Milk"), _ct);
        await CreateService().CreateAsync(user.Id, Req("Errands", "get MILK and eggs"), _ct);
        await CreateService().CreateAsync(user.Id, Req("Unrelated"), _ct);

        var result = await CreateService().ListAsync(user.Id, new TodoListQuery { Search = "milk" }, _ct);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Search_treats_like_wildcards_literally()
    {
        var user = await _db.AddUserAsync();
        await CreateService().CreateAsync(user.Id, Req("Save 100% of it"), _ct);
        await CreateService().CreateAsync(user.Id, Req("Save 1000 of it"), _ct);

        var result = await CreateService().ListAsync(user.Id, new TodoListQuery { Search = "100%" }, _ct);

        Assert.Equal("Save 100% of it", Assert.Single(result.Items).Title);
    }
}
