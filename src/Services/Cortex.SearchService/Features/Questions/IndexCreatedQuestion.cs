using Cortex.Contracts;
using Cortex.SearchService.Infrastructure;
using Typesense;

namespace Cortex.SearchService.Features.Questions;

/// <summary>
/// Adds a newly created question to the search index, from the Question service's
/// <see cref="QuestionCreated"/> event (queue <c>questions.search</c>).
/// </summary>
public static class IndexCreatedQuestionHandler
{
    /// <remarks>
    /// An upsert, not a create: RabbitMQ delivers at least once, so the same event can arrive twice.
    /// A create would fail on the second delivery and end in the dead-letter queue; an upsert just
    /// writes the same document again.
    /// </remarks>
    public static async Task HandleAsync(
        QuestionCreated message,
        ITypesenseClient client,
        CancellationToken ct)
    {
        var createdAt = message.CreatedAt.ToUnixTimeSeconds();

        var document = new SearchQuestion
        {
            Id = message.QuestionId.ToString(),
            Title = message.Title,
            // Bodies are Markdown; the index holds the plain text a reader sees.
            Body = MarkdownText.ToPlainText(message.Body),
            Tags = [.. message.TagSlugs],
            AuthorId = message.AuthorId,
            AuthorDisplayName = message.AuthorDisplayName,
            BookId = message.BookId,
            CreatedAt = createdAt,
            // A new question's last activity is its creation; 0 would sort it last under "recently active".
            LastActivityAt = createdAt
        };

        await client.UpsertDocument(SearchQuestion.CollectionName, document, ct);
    }
}
