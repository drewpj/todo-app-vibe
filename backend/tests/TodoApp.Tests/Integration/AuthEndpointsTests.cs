using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TodoApp.Api.Contracts;
using TodoApp.Tests.Support;
using static TodoApp.Tests.Support.ApiTestHelpers;

namespace TodoApp.Tests.Integration;

public class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_returns_201_with_a_token_that_authenticates_subsequent_calls()
    {
        var email = UniqueEmail();

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, ValidPassword));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.Equal(email, auth.User.Email);
        Assert.True(auth.ExpiresAt > DateTime.UtcNow);

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var meResponse = await _client.SendAsync(me);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal(auth.User.Id, (await meResponse.Content.ReadFromJsonAsync<UserDto>())!.Id);
    }

    [Fact]
    public async Task Register_never_returns_the_password_or_its_hash()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(UniqueEmail(), ValidPassword));

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(ValidPassword, body);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_with_existing_email_returns_409_regardless_of_case()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, ValidPassword));

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email.ToUpperInvariant(), ValidPassword));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "long-enough-password")]
    [InlineData("valid@example.com", "short")]
    [InlineData("", "long-enough-password")]
    public async Task Register_with_invalid_input_returns_400_validation_problem(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotEmpty(problem!.Errors);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, ValidPassword));

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, ValidPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Login_with_bad_credentials_returns_401(bool userExists)
    {
        var email = UniqueEmail();
        if (userExists)
        {
            await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, ValidPassword));
        }

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/todos")]
    public async Task Protected_endpoints_return_401_without_a_token(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_return_401_for_a_tampered_token()
    {
        var (client, auth) = await factory.CreateAuthenticatedClientAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken + "x");

        var response = await client.GetAsync("/api/todos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Auth_endpoints_are_rate_limited()
    {
        using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimiting:AuthPermitLimit"] = "3" })));
        var client = limited.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(UniqueEmail(), "whatever-pass"));
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Unauthorized));
        Assert.Equal(2, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
