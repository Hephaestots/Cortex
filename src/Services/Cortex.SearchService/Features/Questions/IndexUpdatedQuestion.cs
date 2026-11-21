using Cortex.Contracts.Features.Questions;
using Cortex.SearchService.Infrastructure;
using Typesense;

namespace Cortex.SearchService.Features.Questions;

/// <summary>
/// Applies an edit to a question in the search index, from the Question service's
/// <see cref="QuestionUpdated"/> event (queue <c>questions.search</c>).
/// </summary>
public static class IndexUpdatedQuestionHandler
{
    /// <remarks>
    /// <para>
    /// Reads the indexed version first and skips an event that is not newer: RabbitMQ can redeliver an
    /// edit, or deliver an older one after a newer one. Then a partial update (PATCH), so only the
    /// fields an edit owns change. A document that doesn't exist yet is a 404, which ends in the
    /// dead-letter queue on purpose: replay the create, then this update.
    /// </para>
    /// <para>
    /// The read and the write are not atomic (Typesense has no conditional update). That is safe only
    /// while one listener handles <c>questions.search</c> one message at a time, as
    /// <c>ProcessInline</c> does today.
    /// </para>
    /// </remarks>
    public static async Task HandleAsync(
        QuestionUpdated message,
        ITypesenseClient client,
        CancellationToken ct)
    {
        var id = message.QuestionId.ToString();

        var indexed = await client.RetrieveDocument<SearchQuestionVersion>(
            SearchQuestion.CollectionName,
            id,
            ct);

        if (indexed.Version >= message.Version)
            return;

        var document = new SearchQuestionEdit
        {
            Title = message.Title,
            Body = MarkdownText.ToPlainText(message.Body),
            Tags = [.. message.TagSlugs],
            LastActivityAt = message.UpdatedAt.ToUnixTimeSeconds(),
            Version = message.Version
        };

        await client.UpdateDocument(SearchQuestion.CollectionName, id, document, ct);
    }
}
