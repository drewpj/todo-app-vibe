using System.Text;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TodoApp.Api.Auth;
using TodoApp.Api.Domain;
using TodoApp.Api.Options;
using TodoApp.Tests.Support;

namespace TodoApp.Tests.Unit;

public class TokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        SigningKey = ApiFactory.SigningKey,
        Issuer = "test-issuer",
        Audience = "test-audience",
        ExpiryMinutes = 15,
    };

    [Fact]
    public async Task Issued_token_is_valid_and_carries_user_identity_and_expiry()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        var service = new TokenService(Microsoft.Extensions.Options.Options.Create(Options), clock);
        var user = new User { Email = "a@example.com", PasswordHash = "x" };

        var token = service.Issue(user);

        Assert.Equal(new DateTime(2026, 3, 1, 9, 15, 0, DateTimeKind.Utc), token.ExpiresAt);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.SigningKey)),
            // Frozen fake clock is in the past relative to real time, so skip lifetime here; expiry asserted above.
            ValidateLifetime = false,
        });

        Assert.True(result.IsValid, result.Exception?.Message);
        var jwt = (JsonWebToken)result.SecurityToken;
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal("a@example.com", jwt.GetClaim(JwtRegisteredClaimNames.Email).Value);
    }

    [Fact]
    public async Task Token_signed_with_a_different_key_is_rejected()
    {
        var service = new TokenService(Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System);
        var token = service.Issue(new User { Email = "a@example.com", PasswordHash = "x" });

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-completely-different-key-of-sufficient-length")),
        });

        Assert.False(result.IsValid);
    }
}
