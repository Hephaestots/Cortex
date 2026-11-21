using Cortex.Contracts.Features.Questions;
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
    /// A create, not an upsert: RabbitMQ delivers at least once, and a create redelivered after an
    /// edit would upsert the original title and body back over it. A 409 means the document is
    /// already there, from this event or a newer one, so it counts as done.
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
            LastActivityAt = createdAt,
            Version = message.Version
        };

        try
        {
            await client.CreateDocument(SearchQuestion.CollectionName, document, ct);
        }
        catch (TypesenseApiConflictException)
        {
            // Already indexed: see the remarks.
        }
    }
}
