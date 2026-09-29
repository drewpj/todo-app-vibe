using System.Net.Http.Headers;
using System.Net.Http.Json;
using TodoApp.Api.Contracts;

namespace TodoApp.Tests.Support;

public static class ApiTestHelpers
{
    public const string ValidPassword = "correct-horse-battery";

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    /// <summary>Registers a fresh user and returns a client that sends that user's bearer token.</summary>
    public static async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedClientAsync(
        this ApiFactory factory, string? email = null)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email ?? UniqueEmail(), ValidPassword));
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    public static async Task<TodoDto> CreateTodoAsync(
        this HttpClient client, string title = "Buy milk", string? description = null, DateTime? dueDate = null)
    {
        var response = await client.PostAsJsonAsync("/api/todos", new SaveTodoRequest(title, description, dueDate));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TodoDto>())!;
    }

    public static async Task<PagedResult<TodoDto>> ListTodosAsync(this HttpClient client, string query = "")
    {
        var result = await client.GetFromJsonAsync<PagedResult<TodoDto>>($"/api/todos{query}");
        return result!;
    }
}
