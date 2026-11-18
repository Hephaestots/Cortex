using Cortex.QuestionService.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Infrastructure;

/// <summary>
/// Optimistic concurrency for <see cref="IVersioned"/> entities. Two checks give one answer:
/// <list type="number">
/// <item>The endpoint's <c>Validate</c> step compares the version the client sent with the one
/// it just loaded (<see cref="IsStale"/>) and returns 409. This is the common case: someone
/// else saved while the client had the old data on screen.</item>
/// <item>EF adds <c>AND xmin = &lt;loaded version&gt;</c> to the UPDATE or DELETE, so a write
/// that lands between the load and the save fails with <see cref="DbUpdateConcurrencyException"/>,
/// which <see cref="ConcurrencyExceptionHandler"/> turns into the same 409.</item>
/// </list>
/// </summary>
internal static class Concurrency
{
    public static readonly DomainError StaleVersion = new(
        "concurrency.stale_version",
        "Someone else changed this since you loaded it. Reload it and apply your change again.",
        DomainErrorKind.Conflict);

    public static bool IsStale(this IVersioned entity, uint expectedVersion) =>
        entity.Version != expectedVersion;
}

/// <summary>
/// Turns a lost race between load and save into a 409, with the same body as the one the
/// <c>Validate</c> step returns.
/// </summary>
internal sealed class ConcurrencyExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateConcurrencyException)
            return false;

        var error = Concurrency.StaleVersion;
        var statusCode = Problems.StatusCodeFor(error.Kind);

        httpContext.Response.StatusCode = statusCode;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Detail = error.Message,
                Extensions = { ["code"] = error.Code }
            }
        });
    }
}
