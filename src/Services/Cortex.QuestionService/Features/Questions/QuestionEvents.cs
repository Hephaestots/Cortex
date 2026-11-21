using Cortex.Contracts;
using Cortex.Contracts.Features.Questions;
using Cortex.QuestionService.Domain;

namespace Cortex.QuestionService.Features.Questions;

/// <summary>
/// Maps a question to the integration events it publishes, so endpoints return
/// <c>question.ToQuestionCreated()</c> instead of building the event inline.
/// </summary>
/// <remarks>
/// <para>
/// Not on <see cref="Question"/>: the domain knows nothing of Wolverine or <c>Cortex.Contracts</c>, and
/// events are a contract with other services, shaped for them. Not in <c>Cortex.Contracts</c> either:
/// contracts can't reference a service's domain. So the mapping sits here, at the edge, next to the
/// endpoints that publish, like <c>CreateQuestionResponse.From</c> for the HTTP response.
/// </para>
/// <para>
/// Call it after <c>SaveChangesAsync</c>: the created and updated events carry the question's
/// <c>Version</c>, which the database sets on every save and EF reads back.
/// </para>
/// </remarks>
public static class QuestionEvents
{
    extension(Question question)
    {
        public QuestionCreated ToQuestionCreated() => 
            new(
                question.Id,
                question.Title,
                question.Body,
                [.. question.Tags.Select(tag => tag.Slug)],
                question.AuthorId,
                question.AuthorDisplayName,
                question.BookId,
                question.CreatedAt,
                question.Version);

        public QuestionUpdated ToQuestionUpdated() =>
            new(
                question.Id,
                question.Title,
                question.Body,
                [.. question.Tags.Select(tag => tag.Slug)],
                question.UpdatedAt!.Value,
                question.Version);

        public QuestionDeleted ToQuestionDeleted() =>
            new(question.Id);
    }
}
