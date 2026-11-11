using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Account;

public sealed record MeResponse(string? UserId, string? UserName, string? Email, string[] Roles);
public static class MeEndpoint
{
    [Authorize]
    [WolverineGet("/api/account/me")]
    public static MeResponse Get(ClaimsPrincipal user) =>
        new(
            UserId: user.FindFirstValue("sub"),
            UserName: user.Identity?.Name,
            Email: user.FindFirstValue("email"),
            Roles: [.. user.FindAll("roles").Select(role => role.Value)]);
}