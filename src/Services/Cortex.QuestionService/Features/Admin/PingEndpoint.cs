using Cortex.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Admin;

public sealed record PingResponse(string Message, DateTimeOffset At);
public static class PingEndpoint
{
    [Authorize(Policy = Policies.AdminOnly)]
    [WolverineGet("/api/admin/ping")]
    public static PingResponse Get() => new("Pong",  DateTimeOffset.Now);
}