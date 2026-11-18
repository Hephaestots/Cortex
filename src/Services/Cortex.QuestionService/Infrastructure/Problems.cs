using Cortex.QuestionService.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cortex.QuestionService.Infrastructure;

/// <summary>
/// The error responses endpoints return themselves, so they have the same shape as the ones
/// <see cref="DomainExceptionHandler"/> and <see cref="ConcurrencyExceptionHandler"/> write.
/// </summary>
internal static class Problems
{
    /// <summary>The status code for each kind of domain error. Shared with the exception handlers.</summary>
    public static int StatusCodeFor(DomainErrorKind kind) => kind switch
    {
        DomainErrorKind.Validation => StatusCodes.Status400BadRequest,
        DomainErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        DomainErrorKind.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };

    /// <summary>
    /// A <c>ProblemDetails</c> for an expected failure an endpoint returns instead of throwing
    /// (no permission, a stale version): the error's message in <c>detail</c>, its code in
    /// <c>code</c>.
    /// </summary>
    public static ProblemHttpResult FromError(DomainError error) =>
        TypedResults.Problem(
            statusCode: StatusCodeFor(error.Kind),
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    /// <summary>
    /// A 400 with the same shape as the FluentValidation middleware's, for a check the validator
    /// cannot do (one that needs the database). Pass the key with <c>nameof(...)</c> so it matches
    /// the validator's keys, which are the C# property names.
    /// </summary>
    public static ValidationProblem Validation(string key, IEnumerable<string> messages) =>
        TypedResults.ValidationProblem(
            errors: new Dictionary<string, string[]> { [key] = [.. messages] });
}
