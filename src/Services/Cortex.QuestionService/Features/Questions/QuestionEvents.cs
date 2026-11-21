using Cortex.Contracts;
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
/// Call it after <c>SaveChangesAsync</c> when an event carries a database-generated value (a version,
/// say), so the value is read back first. <see cref="QuestionCreated"/> carries none today.
/// </para>
/// </remarks>
public static class QuestionEvents
{
    public static QuestionCreated ToQuestionCreated(this Question question) => new(
        question.Id,
        question.Title,
        question.Body,
        [.. question.Tags.Select(tag => tag.Slug)],
        question.AuthorId,
        question.AuthorDisplayName,
        question.BookId,
        question.CreatedAt);
}
