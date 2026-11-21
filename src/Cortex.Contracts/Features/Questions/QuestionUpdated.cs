namespace Cortex.Contracts.Features.Questions;

/// <summary>
/// Published by the Question service when a question's title, body or tags are edited. Carries the
/// new values of those three only: the author and the book never change after creation.
/// <c>Version</c> works as in <see cref="QuestionCreated"/>.
/// </summary>
public sealed record QuestionUpdated(
    Guid QuestionId,
    string Title,
    string Body,
    IReadOnlyList<string> TagSlugs,
    DateTimeOffset UpdatedAt,
    uint Version);
