using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Stands in for Entra JWT validation in tests. Tests set the "X-Test-Roles" header
/// (comma-separated) to simulate a token's roles claim; omitting it simulates an
/// unauthenticated request, exercising the fail-closed FallbackPolicy.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out var rolesHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var roles = rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        var claims = new List<Claim>
        {
            new("oid", Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, "Test User")
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
