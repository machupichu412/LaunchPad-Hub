using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LaunchPad.Api.LocalDemo;

/// <summary>
/// Local-only stand-in for an Entra token: the caller names a persona from DevPersonas in
/// the X-Dev-Persona header and gets that persona's fixed object id and roles. Registered
/// by Program.cs only when the environment is Development AND Auth:UseDevPersonas is set;
/// the flag outside Development fails startup. The API's authorization policies run
/// unchanged on top of it — this replaces token validation, nothing else.
/// </summary>
public sealed class DevPersonaAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevPersona";
    /// <summary>Picks DevPersona when the header is present, JwtBearer otherwise.</summary>
    public const string SelectorSchemeName = "DevPersonaOrJwt";
    public const string HeaderName = "X-Dev-Persona";

    public DevPersonaAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var key)
            || !DevPersonas.ByKey.TryGetValue(key.ToString(), out var persona))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("oid", persona.EntraObjectId.ToString()),
            new("name", persona.DisplayName),
            new("preferred_username", persona.Upn),
        };
        claims.AddRange(persona.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, SchemeName, "name", ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
