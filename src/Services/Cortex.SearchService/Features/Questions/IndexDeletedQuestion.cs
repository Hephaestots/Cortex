using Cortex.Contracts.Features.Questions;
using Typesense;

namespace Cortex.SearchService.Features.Questions;

/// <summary>
/// Removes a question from the search index, from the Question service's
/// <see cref="QuestionDeleted"/> event (queue <c>questions.search</c>).
/// </summary>
public static class IndexDeletedQuestionHandler
{
    /// <remarks>
    /// RabbitMQ delivers at least once, so the same event can arrive twice. The second delete gets
    /// a 404, which would end in the dead-letter queue; the document is gone either way, so a 404
    /// counts as done.
    /// </remarks>
    public static async Task HandleAsync(
        QuestionDeleted message,
        ITypesenseClient client,
        CancellationToken ct)
    {
        try
        {
            await client.DeleteDocument<SearchQuestion>(
                SearchQuestion.CollectionName, 
                message.QuestionId.ToString(), 
                ct);
        }
        catch (TypesenseApiNotFoundException)
        {
            // Already deleted: see the remarks.
        }
    }
}