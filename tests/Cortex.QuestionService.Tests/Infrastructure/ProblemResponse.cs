namespace Cortex.QuestionService.Tests.Infrastructure;

/// <summary>
/// The parts of an error body the tests check. One record for both shapes the service writes:
/// a <c>ProblemDetails</c> from <c>Problems.FromError</c> or an exception handler (<see cref="Detail"/>
/// and <see cref="Code"/>), and a validation problem (<see cref="Errors"/>, keyed by the C# property name).
/// </summary>
public sealed record ProblemResponse(
    int Status,
    string? Detail,
    string? Code,
    Dictionary<string, string[]>? Errors)
{
    /// <summary>The messages under one validation key, or an empty list.</summary>
    public IReadOnlyList<string> ErrorsFor(string key) =>
        Errors is not null && Errors.TryGetValue(key, out var messages) ? messages : [];
}

public static class ProblemResponseExtensions
{
    public static async Task<ProblemResponse> ReadProblemAsync(this IScenarioResult result) =>
        await result.ReadAsJsonAsync<ProblemResponse>()
        ?? throw new InvalidOperationException("The response has no problem body.");
}
