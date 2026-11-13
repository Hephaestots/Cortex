using System.Security.Claims;
using Cortex.ServiceDefaults;

namespace Cortex.QuestionService.Infrastructure;

/// <summary>
/// The caller of the current request, read from the validated JWT.
/// Take it as an endpoint method parameter instead of parsing claims in each endpoint.
/// </summary>
/// <remarks>
/// It has a plain constructor (no factory lambda in DI) on purpose: Wolverine generates the
/// endpoint code and can then build it inline instead of falling back to service location.
/// </remarks>
public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// The Keycloak user id (<c>sub</c>). Only read it in endpoints marked <c>[Authorize]</c>:
    /// it throws when there is no authenticated user, so a missing attribute fails loudly
    /// instead of storing an empty author id.
    /// </summary>
    public string Id =>
        Principal?.FindFirstValue("sub")
        ?? throw new InvalidOperationException(
            "No authenticated user on this request. Is the endpoint missing [Authorize]?");

    /// <summary><c>preferred_username</c>, as configured in AddCortexAuth.</summary>
    public string? DisplayName => Principal?.Identity?.Name;

    public string? Email => Principal?.FindFirstValue("email");

    /// <summary>The flat <c>roles</c> claim (see "Auth" in CLAUDE.md).</summary>
    public IReadOnlyList<string> Roles =>
        Principal?.FindAll("roles").Select(role => role.Value).ToArray() ?? [];

    public bool IsAdmin => Principal?.IsInRole(ServiceDefaults.Roles.Admin) ?? false;
}
