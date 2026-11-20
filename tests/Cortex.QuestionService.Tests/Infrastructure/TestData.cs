using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Features.Answers;
using Cortex.QuestionService.Features.Questions;

namespace Cortex.QuestionService.Tests.Infrastructure;

/// <summary>
/// Setup steps shared by the endpoint tests. Data goes in through the HTTP API wherever an
/// endpoint exists, so setup exercises the same path as the code under test; only what has no
/// endpoint yet (extra tags, votes, accepting an answer) is written with the DbContext.
/// </summary>
public static class TestData
{
    /// <summary>A seeded tag, for tests that need any valid tag and filter on nothing.</summary>
    public const string AnyTag = "fantasy";

    /// <summary>
    /// Inserts a tag only the calling test knows, and returns its slug. The database is shared,
    /// so a test that filters or counts questions tags them with its own tag and filters on it.
    /// </summary>
    public static async Task<string> CreateTagAsync(this AppFixture fixture)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var tag = new Tag($"Test {suffix}", $"test-{suffix}", "Created by a test.");

        await fixture.WithDbAsync(db =>
        {
            db.Tags.Add(tag);
            return db.SaveChangesAsync();
        });

        return tag.Slug;
    }

    public static async Task<CreateQuestionResponse> CreateQuestionAsync(
        this AppFixture fixture,
        TestUser? author = null,
        IReadOnlyList<string>? tags = null,
        string? title = null,
        string? bookId = null)
    {
        var request = new CreateQuestionRequest(
            title ?? $"Question {Guid.NewGuid():N}",
            "What do you make of the ending?",
            bookId,
            tags ?? [AnyTag]);

        var result = await fixture.Host.Scenario(s =>
        {
            s.As(author ?? TestUsers.Author);
            s.Post.Json(request).ToUrl("/api/questions");
            s.StatusCodeShouldBe(201);
        });

        return await result.ReadAsJsonAsync<CreateQuestionResponse>()
               ?? throw new InvalidOperationException("POST /api/questions returned no body.");
    }

    public static async Task<CreateAnswerResponse> CreateAnswerAsync(
        this AppFixture fixture,
        Guid questionId,
        TestUser? author = null,
        string body = "It reads differently once you know the ending.")
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.As(author ?? TestUsers.Reader);
            s.Post.Json(new CreateAnswerRequest(body)).ToUrl($"/api/questions/{questionId}/answers");
            s.StatusCodeShouldBe(201);
        });

        return await result.ReadAsJsonAsync<CreateAnswerResponse>()
               ?? throw new InvalidOperationException("POST .../answers returned no body.");
    }

    public static async Task<QuestionDetail> GetQuestionAsync(this AppFixture fixture, Guid id)
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/questions/{id}");
            s.StatusCodeShouldBeOk();
        });

        return await result.ReadAsJsonAsync<QuestionDetail>()
               ?? throw new InvalidOperationException($"GET /api/questions/{id} returned no body.");
    }

    /// <summary>Runs <paramref name="work"/> with a fresh DbContext in its own scope.</summary>
    public static async Task WithDbAsync(this AppFixture fixture, Func<QuestionDbContext, Task> work)
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<QuestionDbContext>());
    }

    public static async Task<T> WithDbAsync<T>(this AppFixture fixture, Func<QuestionDbContext, Task<T>> work)
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<QuestionDbContext>());
    }
}
