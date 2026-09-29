using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Api.Contracts;
using TodoApp.Tests.Support;
using static TodoApp.Tests.Support.ApiTestHelpers;

namespace TodoApp.Tests.Integration;

public class TodoEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Create_returns_201_with_location_and_the_new_todo()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();
        var due = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var response = await client.PostAsJsonAsync("/api/todos", new SaveTodoRequest("  Ship it ", "details", due));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = (await response.Content.ReadFromJsonAsync<TodoDto>())!;
        Assert.Equal("Ship it", todo.Title);
        Assert.Equal("details", todo.Description);
        Assert.Equal(due, todo.DueDate);
        Assert.False(todo.IsCompleted);
        Assert.EndsWith($"/api/todos/{todo.Id}", response.Headers.Location!.AbsolutePath);

        var fetched = await client.GetFromJsonAsync<TodoDto>(response.Headers.Location);
        Assert.Equal(todo.Id, fetched!.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_with_blank_title_returns_400(string title)
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/todos", new SaveTodoRequest(title, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, k => k.Equals("Title", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_with_overlong_title_returns_400()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/todos", new SaveTodoRequest(new string('a', 201), null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_replaces_editable_fields()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();
        var todo = await client.CreateTodoAsync("Old", "old description");

        var response = await client.PutAsJsonAsync($"/api/todos/{todo.Id}", new SaveTodoRequest("New", null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<TodoDto>())!;
        Assert.Equal("New", updated.Title);
        Assert.Null(updated.Description);
    }

    [Fact]
    public async Task Update_with_invalid_body_returns_400()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();
        var todo = await client.CreateTodoAsync();

        var response = await client.PutAsJsonAsync($"/api/todos/{todo.Id}", new SaveTodoRequest("", null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Completion_can_be_toggled_on_and_off()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();
        var todo = await client.CreateTodoAsync();

        var done = await (await client.PatchAsJsonAsync($"/api/todos/{todo.Id}/completion", new SetCompletionRequest(true)))
            .Content.ReadFromJsonAsync<TodoDto>();
        var undone = await (await client.PatchAsJsonAsync($"/api/todos/{todo.Id}/completion", new SetCompletionRequest(false)))
            .Content.ReadFromJsonAsync<TodoDto>();

        Assert.True(done!.IsCompleted);
        Assert.NotNull(done.CompletedAt);
        Assert.False(undone!.IsCompleted);
        Assert.Null(undone.CompletedAt);
    }

    [Fact]
    public async Task Delete_returns_204_then_the_todo_is_gone()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();
        var todo = await client.CreateTodoAsync();

        var delete = await client.DeleteAsync($"/api/todos/{todo.Id}");
        var get = await client.GetAsync($"/api/todos/{todo.Id}");
        var deleteAgain = await client.DeleteAsync($"/api/todos/{todo.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteAgain.StatusCode);
    }

    [Fact]
    public async Task Get_unknown_id_returns_404_problem_details()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/todos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task List_supports_status_filter_search_and_paging()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();
        var groceries = await client.CreateTodoAsync("Buy groceries");
        await client.CreateTodoAsync("Call mom");
        await client.CreateTodoAsync("Pay rent");
        await client.PatchAsJsonAsync($"/api/todos/{groceries.Id}/completion", new SetCompletionRequest(true));

        var active = await client.ListTodosAsync("?status=active");
        var completed = await client.ListTodosAsync("?status=completed");
        var search = await client.ListTodosAsync("?search=MOM");
        var paged = await client.ListTodosAsync("?page=2&pageSize=2");

        Assert.Equal(2, active.TotalCount);
        Assert.Equal("Buy groceries", Assert.Single(completed.Items).Title);
        Assert.Equal("Call mom", Assert.Single(search.Items).Title);
        Assert.Equal(3, paged.TotalCount);
        Assert.Single(paged.Items);
    }

    [Theory]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?page=0")]
    [InlineData("?status=bogus")]
    public async Task List_with_invalid_query_returns_400(string query)
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/todos{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
