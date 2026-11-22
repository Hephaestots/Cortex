namespace Cortex.SearchService.Tests.Infrastructure;

/// <summary>
/// The parts of a validation problem the tests check: the messages keyed by the C# property name.
/// A copy of the Question tests' record, cut down: test projects share no code, like the services.
/// </summary>
public sealed record ProblemResponse(int Status, Dictionary<string, string[]>? Errors)
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
