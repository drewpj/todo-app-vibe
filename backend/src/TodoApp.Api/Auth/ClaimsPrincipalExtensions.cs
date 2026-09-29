using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace TodoApp.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The authenticated user's id, taken from the validated token's <c>sub</c> claim.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Authenticated principal has no valid 'sub' claim.");
    }
}
