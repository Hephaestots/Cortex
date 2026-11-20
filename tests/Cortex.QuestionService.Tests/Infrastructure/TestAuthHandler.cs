using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cortex.QuestionService.Tests.Infrastructure;

/// <summary>
/// Replaces Keycloak JWT validation in tests. A request carrying <see cref="HeaderName"/>
/// is signed in as that test user; a request without it stays anonymous, so the endpoint's
/// <c>[Authorize]</c> still answers 401.
/// </summary>
/// <remarks>
/// What this skips is token validation itself (signature, audience, issuer, expiry). That is
/// Keycloak and <c>AddCortexAuth</c> configuration, checked by hand with real tokens. What it
/// keeps is everything the service does with the claims: <c>CurrentUser</c>, roles and policies.
/// The claim types match the real token: <c>sub</c>, <c>preferred_username</c> as the name and a
/// flat <c>roles</c> claim, with the same name and role claim types as <c>AddCortexAuth</c>.
/// </remarks>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string HeaderName = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var userName))
            return Task.FromResult(AuthenticateResult.NoResult());

        var user = TestUsers.All.SingleOrDefault(u => u.UserName == userName);

        if (user is null)
            return Task.FromResult(AuthenticateResult.Fail($"Unknown test user '{userName}'."));

        List<Claim> claims =
        [
            new("sub", user.Id),
            new("preferred_username", user.UserName),
            .. user.Roles.Select(role => new Claim("roles", role))
        ];

        var identity = new ClaimsIdentity(
            claims, SchemeName, nameType: "preferred_username", roleType: "roles");

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
