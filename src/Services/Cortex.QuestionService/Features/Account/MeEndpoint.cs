using Cortex.QuestionService.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Account;

public sealed record MeResponse(string UserId, string? UserName, string? Email, IReadOnlyList<string> Roles);

public static class MeEndpoint
{
    [Authorize]
    [WolverineGet("/api/account/me")]
    public static MeResponse Get(CurrentUser user) =>
        new(user.Id, user.DisplayName, user.Email, user.Roles);
}
