using System.Net;
using System.Net.Http.Json;
using TodoApp.Api.Contracts;
using TodoApp.Tests.Support;
using static TodoApp.Tests.Support.ApiTestHelpers;

namespace TodoApp.Tests.Integration;

/// <summary>The core security requirement: a user can only ever see and change their own tasks.</summary>
public class TodoOwnershipTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(HttpClient Alice, HttpClient Bob, TodoDto AliceTodo)> ArrangeAsync()
    {
        var (alice, _) = await factory.CreateAuthenticatedClientAsync();
        var (bob, _) = await factory.CreateAuthenticatedClientAsync();
        var todo = await alice.CreateTodoAsync("Alice's private task", "secret");
        return (alice, bob, todo);
    }

    [Fact]
    public async Task Users_do_not_see_each_others_todos_in_lists()
    {
        var (alice, bob, _) = await ArrangeAsync();
        await bob.CreateTodoAsync("Bob's task");

        var aliceList = await alice.ListTodosAsync();
        var bobList = await bob.ListTodosAsync();

        Assert.Equal("Alice's private task", Assert.Single(aliceList.Items).Title);
        Assert.Equal("Bob's task", Assert.Single(bobList.Items).Title);
    }

    [Fact]
    public async Task Search_does_not_leak_other_users_todos()
    {
        var (_, bob, _) = await ArrangeAsync();

        var result = await bob.ListTodosAsync("?search=Alice");

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Another_users_todo_cannot_be_read()
    {
        var (_, bob, aliceTodo) = await ArrangeAsync();

        var response = await bob.GetAsync($"/api/todos/{aliceTodo.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_users_todo_cannot_be_edited()
    {
        var (alice, bob, aliceTodo) = await ArrangeAsync();

        var response = await bob.PutAsJsonAsync($"/api/todos/{aliceTodo.Id}", new SaveTodoRequest("Hijacked", null, null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var unchanged = await alice.GetFromJsonAsync<TodoDto>($"/api/todos/{aliceTodo.Id}");
        Assert.Equal("Alice's private task", unchanged!.Title);
    }

    [Fact]
    public async Task Another_users_todo_cannot_be_completed()
    {
        var (alice, bob, aliceTodo) = await ArrangeAsync();

        var response = await bob.PatchAsJsonAsync($"/api/todos/{aliceTodo.Id}/completion", new SetCompletionRequest(true));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var unchanged = await alice.GetFromJsonAsync<TodoDto>($"/api/todos/{aliceTodo.Id}");
        Assert.False(unchanged!.IsCompleted);
    }

    [Fact]
    public async Task Another_users_todo_cannot_be_deleted()
    {
        var (alice, bob, aliceTodo) = await ArrangeAsync();

        var response = await bob.DeleteAsync($"/api/todos/{aliceTodo.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var stillThere = await alice.GetAsync($"/api/todos/{aliceTodo.Id}");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }
}
