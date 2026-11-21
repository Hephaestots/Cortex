namespace Cortex.Contracts.Features.Questions;

/// <summary>
/// Published by the Question service when a question is created, through its outbox, to the
/// <c>questions</c> exchange. Carries everything a consumer needs (fat event), so no consumer has to
/// call back into the Question service.
/// </summary>
/// <remarks>
/// Events are immutable and evolve compatibly: add optional fields, never rename or remove one
/// (see CLAUDE.md). Only what happened at creation is here; consumers choose their own defaults for
/// anything else (the search index's score or answer count, say).
/// <c>Version</c> is the question row's version after the save (Postgres <c>xmin</c>): it only grows,
/// so a consumer can tell an older event from a newer one. It is not a counter and has gaps.
/// </remarks>
public sealed record QuestionCreated(
    Guid QuestionId,
    string Title,
    string Body,
    IReadOnlyList<string> TagSlugs,
    string AuthorId,
    string AuthorDisplayName,
    string? BookId,
    DateTimeOffset CreatedAt,
    uint Version);
