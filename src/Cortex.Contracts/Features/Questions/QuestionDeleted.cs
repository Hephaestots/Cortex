namespace Cortex.Contracts.Features.Questions;

/// <summary>
/// Published by the Question service when a question is deleted, with its answers and votes.
/// Only the id: consumers drop whatever they hold for it.
/// </summary>
public sealed record QuestionDeleted(
    Guid QuestionId);
