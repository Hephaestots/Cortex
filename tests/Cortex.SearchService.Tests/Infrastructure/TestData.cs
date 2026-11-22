using Cortex.Contracts.Features.Questions;
using Cortex.SearchService.Features.Questions;
using Typesense;
using Wolverine;

namespace Cortex.SearchService.Tests.Infrastructure;

/// <summary>
/// Events and index access shared by the handler tests. Events go in through Wolverine, as they
/// would from RabbitMQ; documents are read back straight from Typesense.
/// </summary>
public static class TestData
{
    /// <summary>
    /// A valid <see cref="QuestionCreated"/> with a fresh id, so tests never share a document. Change
    /// what a test cares about with <c>with { ... }</c>.
    /// </summary>
    public static QuestionCreated QuestionCreated() => new(
        QuestionId: Guid.CreateVersion7(),
        Title: "Who narrates the second book?",
        Body: "The **first** chapter says one thing.",
        TagSlugs: ["fantasy"],
        AuthorId: "author-1",
        AuthorDisplayName: "reader",
        BookId: "/works/OL27448W",
        // Whole seconds: the index stores Unix seconds, so tests can compare exactly.
        CreatedAt: new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero),
        Version: 100);

    /// <summary>
    /// A valid <see cref="QuestionUpdated"/> for <paramref name="questionId"/>, newer than
    /// <see cref="QuestionCreated"/> (version and time) and different in every field it carries.
    /// </summary>
    public static QuestionUpdated QuestionUpdated(Guid questionId) => new(
        QuestionId: questionId,
        Title: "Who narrates the second book, updated?",
        Body: "The **second** chapter says another.",
        TagSlugs: ["poetry"],
        UpdatedAt: new DateTimeOffset(2026, 11, 21, 12, 10, 0, TimeSpan.Zero),
        Version: 110);

    extension(AppFixture fixture)
    {
        public ITypesenseClient Typesense => fixture.Host.Services.GetRequiredService<ITypesenseClient>();

        /// <summary>
        /// Hands the event to its handler through Wolverine (the generated code, service location and
        /// the error policy's retries run as in production) and returns once it is handled. An
        /// exception the handler still throws after the retries comes back out of this call.
        /// </summary>
        /// <remarks>
        /// <c>InvokeAsync</c>, not <c>InvokeMessageAndWaitAsync</c>: a tracked session follows all of
        /// the host's activity, and with two handler test classes running in parallel on the shared
        /// host one session timed out (5 s). An inline invoke needs no tracking: it returns when the
        /// handler does, and these handlers cascade nothing.
        /// </remarks>
        public async Task SendAsync(object message)
        {
            // IMessageBus is scoped, and Development validates scopes, so not from the root provider.
            await using var scope = fixture.Host.Services.CreateAsyncScope();

            await scope.ServiceProvider
                .GetRequiredService<IMessageBus>()
                .InvokeAsync(message, TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// Writes non-default counts straight to the document, as answer and vote events will, so a
        /// test can check that a handler leaves them alone (a reset to 0 would not show otherwise).
        /// </summary>
        public Task SetCountsAsync(Guid questionId, int score, int answerCount, bool hasAcceptedAnswer) =>
            fixture.Typesense.UpdateDocument(
                SearchQuestion.CollectionName,
                questionId.ToString(),
                new Dictionary<string, object>
                {
                    [SearchQuestion.Fields.Score] = score,
                    [SearchQuestion.Fields.AnswerCount] = answerCount,
                    [SearchQuestion.Fields.HasAcceptedAnswer] = hasAcceptedAnswer
                },
                TestContext.Current.CancellationToken);

        public Task<SearchQuestion> GetQuestionDocumentAsync(Guid questionId) =>
            fixture.Typesense.RetrieveDocument<SearchQuestion>(
                SearchQuestion.CollectionName,
                questionId.ToString(),
                TestContext.Current.CancellationToken);
    }
}
